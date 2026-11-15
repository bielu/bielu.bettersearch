using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.Abstractions.Query;

public interface ISearchQuery : ICreatableSearchQuery
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ISortOrderField> SortOrder { get; set; }
    public List<IAggregationQuery> FacetQueries { get; set; }
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
    public string Index { get; set; }

    /// <summary>The fields to return in each hit. Empty, the default, returns every field.</summary>
    public IEnumerable<string> Properties { get; set; }
}
