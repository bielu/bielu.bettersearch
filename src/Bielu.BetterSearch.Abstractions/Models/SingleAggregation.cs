namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>
/// The result of a filter facet: one <see cref="SingleBucket"/> holding the number of matching documents, whose
/// <see cref="IBucket.Aggregations"/> hold the facet's nested results.
/// </summary>
public class SingleAggregation : IAggregation
{
    public IReadOnlyList<IBucket> Buckets { get; init; } = [];
    public string AggregationName { get; init; }
}
