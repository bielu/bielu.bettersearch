using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The keyed services a routing service has used in its DI scope, each resolved once. Every notification a keyed
/// service sends while one of this router's calls runs on it is passed to <paramref name="subscribe"/>'s observers;
/// notifications from calls made on the keyed service directly are not.
/// </summary>
/// <remarks>
/// A routed call marks itself in an <see cref="AsyncLocal{T}"/>, so concurrent calls, routed or direct, can't pick up
/// each other's notifications.
/// </remarks>
internal sealed class RoutedServices<TService, TCall>(IServiceProvider serviceProvider, Func<TService, Func<TCall?>, IEnumerable<IDisposable>> subscribe)
    where TService : notnull
    where TCall : class
{
    private readonly object _lock = new();
    private readonly Dictionary<string, TService> _services = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _subscriptions = new();
    private readonly AsyncLocal<TCall?> _current = new();
    private bool _disposed;

    /// <summary>Runs <paramref name="action"/> on the service under <paramref name="key"/>, as the routed call <paramref name="call"/>.</summary>
    public async Task<TResult> RunAsync<TResult>(string key, TCall call, Func<TService, Task<TResult>> action)
    {
        var service = Get(key);
        // Set inside this async method, so the mark flows into the keyed service's notifications and is gone when it returns.
        _current.Value = call;
        return await action(service).ConfigureAwait(false);
    }

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
                    _subscriptions.AddRange(subscribe(service, () => _current.Value));
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

/// <summary>Passes each value to <paramref name="onNext"/>; a routed service's streams end with its own scope, so completion is ignored.</summary>
internal sealed class ForwardingObserver<T>(Action<T> onNext) : IObserver<T>
{
    public void OnNext(T value) => onNext(value);

    public void OnError(Exception error)
    {
    }

    public void OnCompleted()
    {
    }
}
