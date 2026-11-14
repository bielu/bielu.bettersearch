namespace Bielu.BetterSearch.Abstractions.Models;

public interface IBucket
{
    public long? TotalOfDocuments { get; set; }
    public IEnumerable<string> Keys { get; set; }

    /// <summary>The buckets of every nested aggregation, in the order of <c>NestedAggregations</c>.</summary>
    public List<IBucket> SubBuckets { get; set; }

    /// <summary>The results of the facet's <c>NestedAggregations</c>, counted over this bucket's documents.</summary>
    IReadOnlyList<IAggregation> Aggregations => [];
}
