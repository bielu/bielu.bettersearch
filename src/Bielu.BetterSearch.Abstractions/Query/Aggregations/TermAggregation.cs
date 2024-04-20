namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    public class TermAggregation : IAggregationQuery
    {
        public string AggregationName { get; set; }
        public string TermFieldName { get; set; }
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();

    }
}