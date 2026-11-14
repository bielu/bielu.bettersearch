using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The observers of one stream, shared by the Core services so every stream keeps one contract: <c>OnNext</c> for each
/// value, <c>OnCompleted</c> once on <see cref="Complete"/>, and an observer that throws never stops the others.
/// </summary>
/// <remarks>Thread-safe. Subscribing the same observer twice notifies it once, until both handles are disposed.</remarks>
#pragma warning disable BIELU004 // The logger is the owning service's, so failures are logged under its category.
internal sealed class ObserverList<T>(ILogger? logger)
#pragma warning restore BIELU004
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly object _lock = new();
    private readonly List<Entry> _entries = new();
    private bool _completed;

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_lock)
        {
            if (!_completed)
            {
                var entry = _entries.Find(e => ReferenceEquals(e.Observer, observer));
                if (entry == null)
                {
                    entry = new Entry(observer);
                    _entries.Add(entry);
                }

                entry.Count++;
                return new Subscription(this, entry);
            }
        }

        // The stream has already ended, so end it for this observer straight away.
        Call(observer, o => o.OnCompleted(), nameof(IObserver<T>.OnCompleted));
        return new Subscription(null, null);
    }

    public void Notify(T value)
    {
        Entry[] snapshot;
        lock (_lock)
        {
            snapshot = _entries.ToArray();
        }

        // Observers run outside the routed call that sent this, so a write an observer starts on a keyed service is
        // not taken for part of it. Only a routing service's forwarding observer is told which call it was.
        var routedCall = RoutedCall.Current;
        if (routedCall is not null)
        {
            RoutedCall.Current = null;
        }

        try
        {
            foreach (var entry in snapshot)
            {
                // Skip an observer that left during this dispatch, so one that unsubscribed, or was completed by a
                // Complete from inside OnNext, gets nothing more.
                if (entry.Active)
                {
                    Call(entry.Observer, o =>
                    {
                        if (o is ForwardingObserver<T> forwarder)
                        {
                            forwarder.OnNext(value, routedCall);
                        }
                        else
                        {
                            o.OnNext(value);
                        }
                    }, nameof(IObserver<T>.OnNext));
                }
            }
        }
        finally
        {
            if (routedCall is not null)
            {
                RoutedCall.Current = routedCall;
            }
        }
    }

    /// <summary>Sends <c>OnCompleted</c> once to every observer and unsubscribes them. Later calls do nothing.</summary>
    public void Complete()
    {
        Entry[] completed;
        lock (_lock)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            completed = _entries.ToArray();
            _entries.Clear();
            foreach (var entry in completed)
            {
                entry.Active = false;
            }
        }

        foreach (var entry in completed)
        {
            Call(entry.Observer, o => o.OnCompleted(), nameof(IObserver<T>.OnCompleted));
        }
    }

    private void Unsubscribe(Entry entry)
    {
        lock (_lock)
        {
            if (entry.Count > 0 && --entry.Count == 0)
            {
                entry.Active = false;
                _entries.Remove(entry);
            }
        }
    }

    private void Call(IObserver<T> observer, Action<IObserver<T>> notification, string name)
    {
        try
        {
            notification(observer);
        }
#pragma warning disable CA1031 // An observer's failure is its own; it must not undo the operation or skip other observers.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "Observer {Observer} of {Stream} threw in {Notification}.",
                observer.GetType().FullName, typeof(T).Name, name);
        }
    }

    private sealed class Entry(IObserver<T> observer)
    {
        public IObserver<T> Observer { get; } = observer;
        public int Count { get; set; }
        public volatile bool Active = true;
    }

    private sealed class Subscription(ObserverList<T>? list, Entry? entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (list != null && entry != null && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                list.Unsubscribe(entry);
            }
        }
    }
}
