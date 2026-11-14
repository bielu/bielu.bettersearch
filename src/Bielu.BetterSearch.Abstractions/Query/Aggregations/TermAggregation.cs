namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    public class TermAggregation : IAggregationQuery
    {
        public string AggregationName { get; set; }
        /// <summary>The number of buckets the engine uses when <see cref="Size"/> is 0.</summary>
        public const int DefaultSize = 10;

        /// <summary>The most buckets <see cref="Size"/> can ask for, Elasticsearch's default <c>search.max_buckets</c>.</summary>
        public const int MaxSize = 65_536;

        public string TermFieldName { get; set; }

        /// <summary>
        /// How many buckets to return, the values with the most documents first; 0, the default, means
        /// <see cref="DefaultSize"/>.
        /// </summary>
        public int Size { get; set; }
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();

    }
}
