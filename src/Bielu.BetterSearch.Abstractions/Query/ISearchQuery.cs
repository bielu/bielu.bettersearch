using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.Abstractions.Query;

public interface ISearchQuery<T> :  ICreatableSearchQuery<Occurance, ISearchSubQuery>,ISearchQuery
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ISortOrderField> SortOrder { get; set; }
    public List<IAggregationQuery> FacetQueries { get; set; }
    public List<IHighlightQuery> HighlightQueries { get; set; }
    public IDictionary<Occurance, ISearchSubQuery> PostFilterQuery { get; set; }
    public IDictionary<Occurance, ISearchSubQuery> Query { get; set; }
    public string Index { get; set; }
    public DateTime? PreviewAt { get; set; }
    void Add(Occurance queryOccurance, ISearchSubQuery booleanQueryQuery);
    public IEnumerable<string> Properties { get; set; }
}

public interface ISearchQuery
{
}
