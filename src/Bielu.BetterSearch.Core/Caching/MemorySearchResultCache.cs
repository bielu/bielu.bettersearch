using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// A <see cref="ISearchResultCache"/> over <see cref="IMemoryCache"/>, for an application that runs as one process,
/// and for tests. It is single-node only: <see cref="RemoveByTagAsync"/> evicts from this process alone, so with
/// several instances use a cache with a backplane, such as FusionCache.
/// </summary>
/// <remarks>
/// Two searches with the same key that miss at the same time both run. A search that started before
/// <see cref="RemoveByTagAsync"/> and ends after it is not served from the cache, so it can't bring back the result
/// the removal evicted.
/// </remarks>
public sealed class MemorySearchResultCache(IMemoryCache memoryCache) : ISearchResultCache
{
    // One token source per tag; RemoveByTagAsync cancels it, which expires every entry created with its token.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tags = new(StringComparer.Ordinal);

    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        SearchCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (memoryCache.TryGetValue(key, out var cached) && cached is T value)
        {
            return value;
        }

        // Taken before the factory runs, so a removal while it runs expires the entry as soon as it is stored.
        var tokens = options.Tags.Distinct(StringComparer.Ordinal)
            .Select(tag => _tags.GetOrAdd(tag, _ => new CancellationTokenSource()).Token)
            .ToList();
        var created = await factory(cancellationToken).ConfigureAwait(false);

        using var entry = memoryCache.CreateEntry(key);
        entry.AbsoluteExpirationRelativeToNow = options.Duration;
        foreach (var token in tokens)
        {
            entry.AddExpirationToken(new CancellationChangeToken(token));
        }

        entry.Value = created;
        return created;
    }

    public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (_tags.TryRemove(tag, out var source))
        {
            source.Cancel();
        }

        return ValueTask.CompletedTask;
    }
}
