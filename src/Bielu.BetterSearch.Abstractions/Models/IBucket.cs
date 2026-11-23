namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>One bucket of a facet result.</summary>
public interface IBucket
{
    /// <summary>The number of documents in the bucket, when the engine reports it.</summary>
    public long? TotalOfDocuments { get; }

    /// <summary>
    /// The results of the facet's <c>NestedAggregations</c>, counted over this bucket's documents. This is the one
    /// place nested facet results are read from.
    /// </summary>
    IReadOnlyList<IAggregation> Aggregations { get; }
}
