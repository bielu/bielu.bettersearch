namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>
/// The result of a filter facet: one <see cref="SingleBucket"/> holding the number of matching documents, whose
/// <see cref="IBucket.Aggregations"/> hold the facet's nested results.
/// </summary>
public class SingleAggregation : IAggregation
{
    /// <summary>The facet's one <see cref="SingleBucket"/>.</summary>
    public IReadOnlyList<IBucket> Buckets { get; init; } = [];
    /// <inheritdoc />
    public required string AggregationName { get; init; }
}
