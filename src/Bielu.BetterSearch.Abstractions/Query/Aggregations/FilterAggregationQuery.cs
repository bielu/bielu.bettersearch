namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    public class FilterAggregationQuery : IAggregationQuery, ICreatableSearchQuery
    {
        public string AggregationName { get; set; }
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();

        /// <summary>The filter's clauses, each combined by its own <see cref="ISearchSubQuery.Occurrence"/>.</summary>
        public IList<ISearchSubQuery> Queries { get; set; } = [];

        /// <inheritdoc />
        public void Add(Occurrence key, ISearchSubQuery value)
        {
            value.Occurrence = key;
            Queries.Add(value);
        }
    }
}
