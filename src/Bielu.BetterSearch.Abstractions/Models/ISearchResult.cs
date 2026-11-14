namespace Bielu.BetterSearch.Abstractions.Models;

public interface ISearchResult
{
    IEnumerable<ISearchHit> Items { get; set; }
    IPagination Pagination { get; set; }

    /// <summary>
    /// The results of the query's <c>FacetQueries</c>, one per facet in the same order, counted over the main query's
    /// matches: a post filter narrows the hits but not these counts. Empty when the query asked for none.
    /// </summary>
    IReadOnlyList<IAggregation> Aggregations => [];
}
