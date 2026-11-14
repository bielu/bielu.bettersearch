using Bielu.BetterSearch.Abstractions.Query;

namespace Bielu.BetterSearch.Abstractions.Caching;

/// <summary>Which searches <c>SearchServiceAsync</c> caches, and for how long. Set through <c>AddSearchCache</c>.</summary>
public class SearchCacheOptions
{
    /// <summary>The tag every entry of <paramref name="index"/> carries, and that writes to the index evict.</summary>
    public static string IndexTag(string? index) => "bettersearch:index:" + (index ?? string.Empty).ToLowerInvariant();

    /// <summary>Whether searches of an index without its own <see cref="SearchCacheIndexOptions.Enabled"/> are cached.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long a result is cached, for an index without its own <see cref="SearchCacheIndexOptions.Duration"/>. Keep
    /// it short: it bounds how stale a result can be after a write the cache can't see.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Settings for single indexes, looked up by index name ignoring case.</summary>
    public IDictionary<string, SearchCacheIndexOptions> Indexes { get; set; } =
        new Dictionary<string, SearchCacheIndexOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds the application's own part to every cache key, such as the tenant, the culture or the user's permissions,
    /// so two callers that must not see each other's results never share an entry. <c>null</c> adds nothing.
    /// </summary>
    public Func<ISearchQuery, string?>? KeyPart { get; set; }

    /// <summary>When it returns <c>true</c> for a query, that search skips the cache: it is neither read nor stored.</summary>
    public Func<ISearchQuery, bool>? Skip { get; set; }

    /// <summary>The settings for <paramref name="index"/>, ignoring case, or <c>null</c> when it has none.</summary>
    public SearchCacheIndexOptions? ForIndex(string? index)
    {
        if (index is null || Indexes is null)
        {
            return null;
        }

        if (Indexes.TryGetValue(index, out var options))
        {
            return options;
        }

        // A dictionary assigned by the caller may compare keys with case, so fall back to a case-insensitive scan.
        return Indexes.FirstOrDefault(entry => string.Equals(entry.Key, index, StringComparison.OrdinalIgnoreCase)).Value;
    }
}

/// <summary>Cache settings for one index, which override <see cref="SearchCacheOptions"/>.</summary>
public class SearchCacheIndexOptions
{
    /// <summary>Whether searches of the index are cached; <c>null</c> uses <see cref="SearchCacheOptions.Enabled"/>.</summary>
    public bool? Enabled { get; set; }

    /// <summary>How long a result is cached; <c>null</c> uses <see cref="SearchCacheOptions.Duration"/>.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Tags added to the index's entries, beside <see cref="SearchCacheOptions.IndexTag"/>.</summary>
    public IList<string> Tags { get; set; } = [];
}
