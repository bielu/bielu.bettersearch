namespace Bielu.BetterSearch.Abstractions.Models;

public class SingleBucket : IBucket
{
    public long? TotalOfDocuments { get; set; }
    public IEnumerable<string> Keys { get; set; }
    public List<IBucket> SubBuckets { get; set; }
    public bool IsComplex { get; set; }
}