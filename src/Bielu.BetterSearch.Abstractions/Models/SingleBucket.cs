namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>The one bucket of a filter facet.</summary>
public class SingleBucket : IBucket
{
    /// <inheritdoc />
    public long? TotalOfDocuments { get; init; }
    /// <inheritdoc />
    public IReadOnlyList<IAggregation> Aggregations { get; init; } = [];
}
