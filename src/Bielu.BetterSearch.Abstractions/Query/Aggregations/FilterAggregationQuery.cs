namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    public class FilterAggregationQuery : IAggregationQuery
    {
        public string AggregationName { get; set; }
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();

        public Dictionary<Occurrence, ISearchSubQuery> Queries { get; set; } =
            new Dictionary<Occurrence, ISearchSubQuery>();
    }
}
