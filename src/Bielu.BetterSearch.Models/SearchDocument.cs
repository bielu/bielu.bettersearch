namespace Bielu.BetterSearch;

public class SearchDocument
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Index { get; set; }
    public IDictionary<string, IList<object>> Fields { get; set; } = new Dictionary<string, IList<object>>();
}
