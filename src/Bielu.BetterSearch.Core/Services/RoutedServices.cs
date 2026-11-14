using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The keyed services a routing service has used in its DI scope, each resolved once. <paramref name="subscribe"/>
/// subscribes the router to each of them, with observers from <see cref="ForwardTo{T}"/> of the instance it is passed,
/// which pass on only the notifications of this router's own calls.
/// </summary>
/// <remarks>
/// A routed call marks itself in <see cref="RoutedCall.Current"/>, so concurrent calls, routed or direct, can't pick
/// up each other's notifications, and <see cref="ObserverList{T}"/> clears the mark while it calls observers, so a
/// write an observer starts on a keyed service isn't taken for part of the routed call.
/// </remarks>
internal sealed class RoutedServices<TService>(IServiceProvider serviceProvider, Func<TService, RoutedServices<TService>, IEnumerable<IDisposable>> subscribe)
    where TService : notnull
{
    private readonly object _lock = new();
    private readonly Dictionary<string, TService> _services = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// Starts a call whose notifications are held until <see cref="RoutedCall.Flush"/>, for an operation that runs on
    /// several keyed services and notifies only when all of them succeed.
    /// </summary>
    public RoutedCall BufferedCall() => new(this, buffered: true);

    /// <summary>
    /// Runs <paramref name="action"/> on the service under <paramref name="key"/>, as part of <paramref name="call"/>,
    /// or as a call of its own whose notifications are passed on straight away.
    /// </summary>
    public async Task<TResult> RunAsync<TResult>(string key, Func<TService, Task<TResult>> action, RoutedCall? call = null)
    {
        var service = Get(key);
        // Set inside this async method, so the mark flows into the keyed service's notifications and is gone when it returns.
        RoutedCall.Current = call ?? new RoutedCall(this, buffered: false);
        return await action(service).ConfigureAwait(false);
    }

    /// <summary>An observer that passes the notifications of this router's calls on to <paramref name="observers"/>.</summary>
    public IObserver<T> ForwardTo<T>(ObserverList<T> observers) =>
        new ForwardingObserver<T>((value, call) =>
        {
            if (call is not null && ReferenceEquals(call.Owner, this))
            {
                call.Forward(() => observers.Notify(value));
            }
        });

    private TService Get(string key)
    {
        lock (_lock)
        {
            if (!_services.TryGetValue(key, out var service))
            {
                service = serviceProvider.GetRequiredKeyedService<TService>(key);
                // Once the router is disposed its observers are completed, so there is nothing to forward to.
                if (!_disposed)
                {
                    _subscriptions.AddRange(subscribe(service, this));
                    _services.Add(key, service);
                }
            }

            return service;
        }
    }

    /// <summary>Stops listening to the keyed services. They belong to the DI scope, which disposes them.</summary>
    public void Dispose()
    {
        IDisposable[] subscriptions;
        lock (_lock)
        {
            _disposed = true;
            subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }
}

/// <summary>A call a routing service runs on keyed services, marked in the async flow while it runs.</summary>
internal sealed class RoutedCall(object owner, bool buffered)
{
    private static readonly AsyncLocal<RoutedCall?> s_current = new();
    private readonly List<Action>? _buffer = buffered ? new() : null;

    /// <summary>The routed call running in this async flow, or <c>null</c> outside one and inside an observer.</summary>
    public static RoutedCall? Current
    {
        get => s_current.Value;
        set => s_current.Value = value;
    }

    /// <summary>The router that started the call.</summary>
    public object Owner { get; } = owner;

    /// <summary>Runs <paramref name="notify"/> now, or holds it until <see cref="Flush"/> for a buffered call.</summary>
    public void Forward(Action notify)
    {
        if (_buffer is null)
        {
            notify();
            return;
        }

        lock (_buffer)
        {
            _buffer.Add(notify);
        }
    }

    /// <summary>Sends the notifications a buffered call has held, in the order they came.</summary>
    public void Flush()
    {
        if (_buffer is null)
        {
            return;
        }

        Action[] held;
        lock (_buffer)
        {
            held = _buffer.ToArray();
            _buffer.Clear();
        }

        foreach (var notify in held)
        {
            notify();
        }
    }
}

/// <summary>
/// Passes each value, with the routed call that sent it, to <paramref name="onNext"/>; a routed service's streams end
/// with its own scope, so completion is ignored.
/// </summary>
internal sealed class ForwardingObserver<T>(Action<T, RoutedCall?> onNext) : IObserver<T>
{
    /// <summary>Called by an observable other than <see cref="ObserverList{T}"/>, which leaves the mark as it is.</summary>
    public void OnNext(T value) => onNext(value, RoutedCall.Current);

    /// <summary>Called by <see cref="ObserverList{T}"/> with the routed call that was running when it notified.</summary>
    public void OnNext(T value, RoutedCall? call) => onNext(value, call);

    public void OnError(Exception error)
    {
    }

    public void OnCompleted()
    {
    }
}
