namespace Bielu.BetterSearch.Abstractions.Query.Aggregations;

public interface IAggregationQuery
{
    string AggregationName { get; set; }
    public List<IAggregationQuery> NestedAggregations{ get; set; }
}
