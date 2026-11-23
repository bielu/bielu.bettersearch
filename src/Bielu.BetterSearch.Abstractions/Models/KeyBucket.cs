namespace Bielu.BetterSearch.Abstractions.Models
{
    /// <summary>The bucket of one value of a term facet, or of one combination of a multi-term facet's values.</summary>
    public class KeyBucket : IBucket
    {
        /// <summary>The bucket's key: the value, or for a multi-term facet the engine's joined key.</summary>
        public required string Key { get; init; }

        /// <summary>The value of each field: one for a term facet, one per term, in order, for a multi-term facet.</summary>
        public IReadOnlyList<string> Keys { get; init; } = [];

        /// <inheritdoc />
        public long? TotalOfDocuments { get; init; }
        /// <inheritdoc />
        public IReadOnlyList<IAggregation> Aggregations { get; init; } = [];
    }
}
