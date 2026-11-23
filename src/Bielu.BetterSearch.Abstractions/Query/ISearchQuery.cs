using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.Abstractions.Query;

/// <summary>A search: its clauses, paging, sorting, facets, highlighting and the index it runs on.</summary>
public interface ISearchQuery : ICreatableSearchQuery
{
    /// <summary>The page to return, counting from 1; 0, the default, is the first page.</summary>
    public int Page { get; set; }
    /// <summary>The number of hits per page; 0 or less, the default, uses the provider's default page size.</summary>
    public int PageSize { get; set; }
    /// <summary>The keys to sort the hits by; empty sorts by relevance.</summary>
    public List<ISortOrderField> SortOrder { get; set; }
    /// <summary>The facets to compute, returned in <see cref="Models.ISearchResult.Aggregations"/> in the same order.</summary>
    public List<IAggregationQuery> FacetQueries { get; set; }
    /// <summary>The fields to highlight, returned in each hit's <see cref="Models.ISearchHit.Highlights"/>.</summary>
    public List<IHighlightQuery> HighlightQueries { get; set; }

    /// <summary>
    /// Clauses that filter the hits after the facets are counted, each combined by its own
    /// <see cref="ISearchSubQuery.Occurrence"/>, as in <see cref="Query"/>.
    /// </summary>
    public IList<ISearchSubQuery> PostFilterQuery { get; set; }

    /// <summary>
    /// The query's clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>, so a top-level
    /// query reads the same as a <see cref="SubQueries.BoolSearchSubQuery"/>.
    /// </summary>
    public IList<ISearchSubQuery> Query { get; set; }
    /// <summary>The name of the index to search, without the provider's index prefix.</summary>
    public string Index { get; set; }

    /// <summary>The fields to return in each hit. Empty, the default, returns every field.</summary>
    public IEnumerable<string> Properties { get; set; }
}
