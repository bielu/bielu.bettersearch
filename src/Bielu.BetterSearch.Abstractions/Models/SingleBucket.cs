namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>The one bucket of a filter facet.</summary>
public class SingleBucket : IBucket
{
    public long? TotalOfDocuments { get; init; }
    public IReadOnlyList<IAggregation> Aggregations { get; init; } = [];
}
