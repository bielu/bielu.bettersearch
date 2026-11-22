namespace Bielu.BetterSearch.Abstractions.Models;

public class BaseSearchResults : ISearchResult
{
    public IReadOnlyList<ISearchHit> Items { get; init; } = [];
    public IPagination Pagination { get; init; } = new Pagination();
    public IReadOnlyList<IAggregation> Aggregations { get; init; } = [];
}
