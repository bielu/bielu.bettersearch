namespace Bielu.BetterSearch.Abstractions.Caching;

/// <summary>
/// The store <c>SearchServiceAsync</c> keeps search results in, when the application registers one with
/// <c>AddSearchCache</c>. Implement it to plug in your own cache.
/// </summary>
/// <remarks>
/// The eviction contract: after <see cref="RemoveByTagAsync"/> returns, no node may return an entry with that tag,
/// apart from the propagation delay of the implementation's backplane. Evicting from this node and a shared
/// distributed cache is not enough, because the other nodes still hold the entry in their local memory. An
/// implementation that can't meet this has to say so in its own docs.
/// </remarks>
public interface ISearchResultCache
{
    /// <summary>
    /// Returns the value cached under <paramref name="key"/>, or runs <paramref name="factory"/>, caches what it returns
    /// with <paramref name="options"/> and returns it. When <paramref name="factory"/> throws, nothing is cached and the
    /// exception reaches the caller, so a failed or cancelled search is never cached.
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        SearchCacheEntryOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts every entry with <paramref name="tag"/> from every node: its local (L1) memory as well as any distributed
    /// (L2) cache, not only from this node.
    /// </summary>
    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
