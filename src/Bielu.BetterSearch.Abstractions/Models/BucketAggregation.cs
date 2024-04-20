namespace Bielu.BetterSearch.Abstractions.Models
{
    public class BucketAggregation : IAggregation
    {
        public List<IBucket> Buckets { get; set; } = new List<IBucket>();
        public string AggregationName { get; set; }
        public IEnumerable<IAggregation> NestedAggregation { get; set; } = new List<IAggregation>();
    }
    public class SingleAggregation : IAggregation
    {
        public List<IBucket> Buckets { get; set; } = new List<IBucket>();
        public string AggregationName { get; set; }
        public IEnumerable<IAggregation> NestedAggregation { get; set; } = new List<IAggregation>();
    }
}