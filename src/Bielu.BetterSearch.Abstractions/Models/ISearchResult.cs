namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>The result of a search: the hits of one page, its pagination and the facet results.</summary>
public interface ISearchResult
{
    /// <summary>The hits of the requested page, in the order the engine ranked them.</summary>
    IReadOnlyList<ISearchHit> Items { get; }

    /// <summary>Where the page of <see cref="Items"/> sits among all the matches.</summary>
    IPagination Pagination { get; }

    /// <summary>
    /// The results of the query's <c>FacetQueries</c>, one per facet in the same order, counted over the main query's
    /// matches: a post filter narrows the hits but not these counts. Empty when the query asked for none.
    /// </summary>
    IReadOnlyList<IAggregation> Aggregations { get; }
}
