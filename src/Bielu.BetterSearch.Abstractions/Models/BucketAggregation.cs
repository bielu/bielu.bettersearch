namespace Bielu.BetterSearch.Abstractions.Models
{
    /// <summary>The result of a term or multi-term facet: one <see cref="KeyBucket"/> per value or combination of values.</summary>
    public class BucketAggregation : IAggregation
    {
        public IReadOnlyList<IBucket> Buckets { get; init; } = [];
        public string AggregationName { get; init; }
    }
}
