namespace Bielu.BetterSearch.Abstractions.Query.Aggregations;

/// <summary>A facet to compute over the main query's matches.</summary>
public interface IAggregationQuery
{
    /// <summary>The name the facet's result is returned under, in <see cref="Models.IAggregation.AggregationName"/>.</summary>
    string AggregationName { get; set; }
    /// <summary>Facets counted over each bucket's documents, returned in <see cref="Models.IBucket.Aggregations"/>.</summary>
    public List<IAggregationQuery> NestedAggregations { get; set; }
}
