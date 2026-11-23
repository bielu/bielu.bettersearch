namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    /// <summary>A facet counting the documents of each value of <see cref="TermFieldName"/>.</summary>
    public class TermAggregation : IAggregationQuery
    {
        /// <inheritdoc />
        public string AggregationName { get; set; } = string.Empty;
        /// <summary>The number of buckets the engine uses when <see cref="Size"/> is 0.</summary>
        public const int DefaultSize = 10;

        /// <summary>The most buckets <see cref="Size"/> can ask for, Elasticsearch's default <c>search.max_buckets</c>.</summary>
        public const int MaxSize = 65_536;

        /// <summary>The name of the field whose values are counted.</summary>
        public string TermFieldName { get; set; } = string.Empty;

        /// <summary>
        /// How many buckets to return, the values with the most documents first; 0, the default, means
        /// <see cref="DefaultSize"/>.
        /// </summary>
        public int Size { get; set; }
        /// <inheritdoc />
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();

    }
}
