namespace Bielu.BetterSearch.Abstractions.Models
{
    public class KeyBucket : IBucket
    {
        public string Key { get; set; }
        public long? TotalOfDocuments { get; set; }
        public IEnumerable<string> Keys { get; set; }
        public List<IBucket> SubBuckets { get; set; } = new List<IBucket>();
        public bool IsComplex { get; set; }
        public IReadOnlyList<IAggregation> Aggregations { get; set; } = [];
    }
}
