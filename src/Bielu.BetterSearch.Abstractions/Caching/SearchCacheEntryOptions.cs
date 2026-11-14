namespace Bielu.BetterSearch.Abstractions.Caching;

/// <summary>How long one search result is cached, and the tags that evict it.</summary>
public sealed class SearchCacheEntryOptions
{
    /// <summary>How long the entry lives after it is created.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The tags <see cref="ISearchResultCache.RemoveByTagAsync"/> evicts the entry by.</summary>
    public IReadOnlyCollection<string> Tags { get; init; } = [];
}
