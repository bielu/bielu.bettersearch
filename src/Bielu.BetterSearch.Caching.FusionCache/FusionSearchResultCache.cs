using Bielu.BetterSearch.Abstractions.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Bielu.BetterSearch.Caching.FusionCache;

/// <summary>
/// A <see cref="ISearchResultCache"/> over <see cref="IFusionCache"/>, using its tagging. With several instances,
/// configure FusionCache with a distributed cache and a backplane, such as Redis: the backplane is what carries a
/// <see cref="RemoveByTagAsync"/> to the local memory of the other nodes. Without one, removal reaches this node and the
/// distributed cache only, and the other nodes serve their copy until it expires.
/// </summary>
public sealed class FusionSearchResultCache(IFusionCache cache) : ISearchResultCache
{
    public async ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        SearchCacheEntryOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        // Fail-safe off: a failed search must reach the caller, not be hidden behind an expired result.
        var entryOptions = cache.CreateEntryOptions(entry => entry.IsFailSafeEnabled = false, options.Duration);
        return await cache.GetOrSetAsync<T>(key, async (_, token) => await factory(token).ConfigureAwait(false),
            options: entryOptions, tags: options.Tags, token: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        await cache.RemoveByTagAsync(tag, token: cancellationToken).ConfigureAwait(false);
}
