namespace Bielu.BetterSearch.Abstractions.Models;

public class SingleBucket : IBucket
{
    public long? TotalOfDocuments { get; set; }
    public IEnumerable<string> Keys { get; set; }
    public List<IBucket> SubBuckets { get; set; } = new List<IBucket>();
    public bool IsComplex { get; set; }
    public IReadOnlyList<IAggregation> Aggregations { get; set; } = [];
}
