namespace Bielu.BetterSearch.Abstractions.Models
{
    public class MultiTermBucket
    {
        public List<string> Keys { get; set; }
        public string Key { get; set; }
        public long? TotalOfDocuments { get; set; }
    }
}