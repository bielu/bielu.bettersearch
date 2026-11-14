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
/// the removal evicted. Each entry has a <see cref="ICacheEntry.Size"/> of 1, so an <see cref="IMemoryCache"/> with a
/// <see cref="MemoryCacheOptions.SizeLimit"/> holds that many searches.
/// </remarks>
public sealed class MemorySearchResultCache(IMemoryCache memoryCache) : ISearchResultCache
{
    // One token source per tag; RemoveByTagAsync cancels it, which expires every entry created with its token. A tag
    // is kept while a search that took its token runs or an entry made with it is stored, and dropped after that, so
    // the tags held are those of the cached entries rather than of every index ever searched.
    private readonly Dictionary<string, TagState> _tags = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>The number of tags held, for tests.</summary>
    internal int TagCount
    {
        get
        {
            lock (_lock)
            {
                return _tags.Count;
            }
        }
    }

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
        var tags = Acquire(options.Tags.Distinct(StringComparer.Ordinal));
        try
        {
            var created = await factory(cancellationToken).ConfigureAwait(false);
            // The entry holds its tags until it leaves the cache, for whatever reason.
            var held = Acquire(tags);
            try
            {
                using var entry = memoryCache.CreateEntry(key);
                entry.AbsoluteExpirationRelativeToNow = options.Duration;
                // An IMemoryCache with a SizeLimit refuses entries without a size; each search counts as one.
                entry.Size = 1;
                foreach (var tag in tags)
                {
                    entry.AddExpirationToken(new CancellationChangeToken(tag.Source.Token));
                }

                entry.RegisterPostEvictionCallback(static (_, _, _, state) => ((TagLease)state!).Release(), held);
                entry.Value = created;
            }
            catch
            {
                held.Release();
                throw;
            }

            return created;
        }
        finally
        {
            Release(tags);
        }
    }

    public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tag);
        TagState? state;
        lock (_lock)
        {
            _tags.Remove(tag, out state);
        }

        state?.Source.Cancel();
        return ValueTask.CompletedTask;
    }

    private List<TagState> Acquire(IEnumerable<string> tags)
    {
        lock (_lock)
        {
            var states = new List<TagState>();
            foreach (var tag in tags)
            {
                if (!_tags.TryGetValue(tag, out var state))
                {
                    state = new TagState(tag);
                    _tags.Add(tag, state);
                }

                state.Users++;
                states.Add(state);
            }

            return states;
        }
    }

    private TagLease Acquire(List<TagState> states)
    {
        lock (_lock)
        {
            foreach (var state in states)
            {
                state.Users++;
            }
        }

        return new TagLease(this, states);
    }

    private void Release(List<TagState> states)
    {
        lock (_lock)
        {
            foreach (var state in states)
            {
                // A tag removed and taken again has a new state, which this release must not drop.
                if (--state.Users == 0 && _tags.TryGetValue(state.Tag, out var current) && current == state)
                {
                    _tags.Remove(state.Tag);
                }
            }
        }
    }

    private sealed class TagState(string tag)
    {
        public string Tag { get; } = tag;
        public CancellationTokenSource Source { get; } = new();
        public int Users { get; set; }
    }

    /// <summary>The tags a stored entry holds; released once, when the entry leaves the cache or is never stored.</summary>
    private sealed class TagLease(MemorySearchResultCache cache, List<TagState> states)
    {
        private int _released;

        public void Release()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                cache.Release(states);
            }
        }
    }
}
