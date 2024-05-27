namespace Bielu.BetterSearch.Abstractions.Models;

public interface IBucket
{
    public long? TotalOfDocuments { get; set; }
    public IEnumerable<string> Keys { get; set; }
    public List<IBucket> SubBuckets { get; set; } 
}