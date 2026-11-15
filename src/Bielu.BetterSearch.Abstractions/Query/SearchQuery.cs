using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.Abstractions.Query
{
    public class SearchQuery : ISearchQuery
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<ISortOrderField> SortOrder { get; set; } = new();
        public List<IAggregationQuery> FacetQueries { get; set; } = new List<IAggregationQuery>();
        public List<IHighlightQuery> HighlightQueries { get; set; } = new List<IHighlightQuery>();

        public IList<ISearchSubQuery> PostFilterQuery { get; set; } = [];

        public IList<ISearchSubQuery> Query { get; set; } = [];
        public string Index { get; set; }

        public IEnumerable<string> Properties { get; set; } = [];

        /// <inheritdoc />
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            value.Occurrence = key;
            Query.Add(value);
        }
    }
}
