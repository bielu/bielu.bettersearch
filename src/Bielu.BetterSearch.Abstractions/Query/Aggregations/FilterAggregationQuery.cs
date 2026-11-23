namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    /// <summary>A facet counting the documents its <see cref="Queries"/> match, with <see cref="NestedAggregations"/> over them.</summary>
    public class FilterAggregationQuery : IAggregationQuery, ICreatableSearchQuery
    {
        /// <inheritdoc />
        public string AggregationName { get; set; } = string.Empty;
        /// <inheritdoc />
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();

        /// <summary>The filter's clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>.</summary>
        public IList<ISearchSubQuery> Queries { get; set; } = [];

        /// <inheritdoc />
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Occurrence = key;
            Queries.Add(value);
        }
    }
}
