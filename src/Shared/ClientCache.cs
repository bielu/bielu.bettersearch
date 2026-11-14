using System.Collections.Concurrent;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// Caches one client per distinct connection key, and keeps only the clients that an index name last resolved to.
/// Index names that resolve to the same key share a client. When an index resolves to a new key, such as after a password
/// or token rotated through <c>IOptionsMonitor</c>, it gets a client for the new key, and the client for the old key is
/// dropped once no index name resolves to it any more, so a credential rotated every few minutes does not grow the cache.
/// A dropped client is not disposed: a caller that already holds it finishes its requests on it, and it is collected
/// once no caller holds it. A call that read the settings just before they changed can bring the old key back for a
/// moment; the next call with the new settings drops it again.
/// </summary>
internal sealed class ClientCache<TKey, TClient>
    where TKey : notnull
    where TClient : class
{
    private readonly ConcurrentDictionary<TKey, Lazy<TClient>> _clients = new();
    // Index names are looked up whatever their case, as the settings managers do.
    private readonly ConcurrentDictionary<string, TKey> _currentKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>The number of clients cached.</summary>
    public int Count => _clients.Count;

    /// <summary>
    /// Gets the client for <paramref name="key"/>, creating it with <paramref name="create"/> when it is not cached, and
    /// records it as <paramref name="indexName"/>'s current client.
    /// </summary>
    /// <remarks>
    /// An exception from <paramref name="create"/> is not cached, so invalid settings fail each call until they are fixed.
    /// Two calls racing to create the same client can each run <paramref name="create"/>, and only one client is kept.
    /// </remarks>
    public TClient GetOrCreate(string indexName, TKey key, Func<TKey, TClient> create)
    {
        if (!_currentKeys.TryGetValue(indexName, out var current) ||
            !EqualityComparer<TKey>.Default.Equals(current, key) ||
            !_clients.TryGetValue(key, out var client))
        {
            lock (_lock)
            {
                _currentKeys[indexName] = key;
                client = _clients.GetOrAdd(key, k => new Lazy<TClient>(() => create(k), LazyThreadSafetyMode.PublicationOnly));
                RemoveUnused();
            }
        }

        // Created outside the lock, so a slow client constructor or configureClient callback does not hold up other indexes.
        return client.Value;
    }

    private void RemoveUnused()
    {
        var used = new HashSet<TKey>(_currentKeys.Values);
        foreach (var key in _clients.Keys)
        {
            if (!used.Contains(key))
            {
                _clients.TryRemove(key, out _);
            }
        }
    }
}
