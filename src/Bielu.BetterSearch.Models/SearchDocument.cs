namespace Bielu.BetterSearch;

public class SearchDocument
{
    public string Id { get; set; }
    public string Type { get; set; }
    public string Index { get; set; }

    /// <summary>The document's fields. Setting <c>null</c>, in code or by deserializing, gives an empty dictionary.</summary>
    public IDictionary<string, IList<object>> Fields
    {
        get;
        set => field = value ?? new Dictionary<string, IList<object>>();
    } = new Dictionary<string, IList<object>>();
}
