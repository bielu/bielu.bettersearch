using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.Abstractions.Query
{
    public class SearchQuery : ISearchQuery<ISearchResult<ISearchModel>>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<ISortOrderField> SortOrder { get; set; } = new();
        public List<IAggregationQuery> FacetQueries { get; set; } = new List<IAggregationQuery>();
        public List<IHighlightQuery> HighlightQueries { get; set; } = new List<IHighlightQuery>();

        public IDictionary<Occurance, ISearchSubQuery> PostFilterQuery { get; set; } =
            new Dictionary<Occurance, ISearchSubQuery>();

        public IDictionary<Occurance, ISearchSubQuery> Query { get; set; }
        public string Index { get; set; }
        public DateTime? PreviewAt { get; set; }


        public IEnumerable<string> Properties { get; set; }
        public void Add(Occurance key, ISearchSubQuery value) =>  Query.Add(key, value);
    }
}
