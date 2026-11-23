namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>The result of a search: the hits of one page, its pagination and the facet results.</summary>
public class BaseSearchResults : ISearchResult
{
    /// <inheritdoc />
    public IReadOnlyList<ISearchHit> Items { get; init; } = [];
    /// <inheritdoc />
    public IPagination Pagination { get; init; } = new Pagination();
    /// <inheritdoc />
    public IReadOnlyList<IAggregation> Aggregations { get; init; } = [];
}
