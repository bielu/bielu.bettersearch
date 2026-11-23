namespace Bielu.BetterSearch.Abstractions.Models
{
    /// <summary>The result of a term or multi-term facet: one <see cref="KeyBucket"/> per value or combination of values.</summary>
    public class BucketAggregation : IAggregation
    {
        /// <summary>The facet's buckets, one per value or combination of values.</summary>
        public IReadOnlyList<IBucket> Buckets { get; init; } = [];
        /// <inheritdoc />
        public required string AggregationName { get; init; }
    }
}
