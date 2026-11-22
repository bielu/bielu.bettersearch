namespace Bielu.BetterSearch.Abstractions.Models;

public interface IBucket
{
    public long? TotalOfDocuments { get; }

    /// <summary>
    /// The results of the facet's <c>NestedAggregations</c>, counted over this bucket's documents. This is the one
    /// place nested facet results are read from.
    /// </summary>
    IReadOnlyList<IAggregation> Aggregations => [];
}
