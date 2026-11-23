using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;

namespace Bielu.BetterSearch.Abstractions.Query
{
    /// <summary>The default <see cref="ISearchQuery"/>.</summary>
    public class SearchQuery : ISearchQuery
    {
        /// <inheritdoc />
        public int Page { get; set; }
        /// <inheritdoc />
        public int PageSize { get; set; }
        /// <inheritdoc />
        public List<ISortOrderField> SortOrder { get; set; } = new();
        /// <inheritdoc />
        public List<IAggregationQuery> FacetQueries { get; set; } = new List<IAggregationQuery>();
        /// <inheritdoc />
        public List<IHighlightQuery> HighlightQueries { get; set; } = new List<IHighlightQuery>();

        /// <inheritdoc />
        public IList<ISearchSubQuery> PostFilterQuery { get; set; } = [];

        /// <inheritdoc />
        public IList<ISearchSubQuery> Query { get; set; } = [];
        /// <inheritdoc />
        public required string Index { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> Properties { get; set; } = [];

        /// <inheritdoc />
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Occurrence = key;
            Query.Add(value);
        }
    }
}
