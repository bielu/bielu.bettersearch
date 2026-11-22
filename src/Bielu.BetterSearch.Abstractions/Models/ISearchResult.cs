namespace Bielu.BetterSearch.Abstractions.Models;

public interface ISearchResult
{
    /// <summary>The hits of the requested page, in the order the engine ranked them.</summary>
    IReadOnlyList<ISearchHit> Items { get; }

    IPagination Pagination { get; }

    /// <summary>
    /// The results of the query's <c>FacetQueries</c>, one per facet in the same order, counted over the main query's
    /// matches: a post filter narrows the hits but not these counts. Empty when the query asked for none.
    /// </summary>
    IReadOnlyList<IAggregation> Aggregations => [];
}
