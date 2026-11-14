namespace Bielu.BetterSearch.Abstractions.Query.Aggregations
{
    public class MultiTermAggreation : IAggregationQuery
    {
        public string AggregationName { get; set; }
        public List<SimpleTerm> Terms { get; set; }

        /// <summary>
        /// How many buckets to return, the combinations with the most documents first; 0, the default, means
        /// <see cref="TermAggregation.DefaultSize"/>.
        /// </summary>
        public int Size { get; set; }
        public List<IAggregationQuery> NestedAggregations { get; set; } = new List<IAggregationQuery>();


    }
}
