namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    /// <summary>A facet counting the documents of each combination of the values of <see cref="Terms"/>.</summary>
    public class MultiTermAggregation : IAggregationQuery
    {
        /// <inheritdoc />
        public required string AggregationName { get; set; }
        /// <summary>The fields whose values are combined, in the order of each bucket's <see cref="Models.KeyBucket.Keys"/>.</summary>
        public List<SimpleTerm> Terms { get; set; } = [];

        /// <summary>
        /// How many buckets to return, the combinations with the most documents first; 0, the default, means
        /// <see cref="TermAggregation.DefaultSize"/>.
        /// </summary>
        public int Size { get; set; }
        /// <inheritdoc />
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();


    }
}
