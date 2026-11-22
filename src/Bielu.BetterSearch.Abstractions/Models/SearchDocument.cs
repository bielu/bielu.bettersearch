namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>
/// One document, in the one shape used to write it (<c>IndexDocumentAsync</c>), to read it by id
/// (<c>GetDocumentAsync</c>) and to read it from a search hit (<see cref="ISearchHit.Document"/>), so a document read
/// either way can be written back as it is.
/// </summary>
public class SearchDocument
{
    /// <summary>The document id, exactly as it was indexed.</summary>
    public string Id { get; set; }

    /// <summary>The document's type.</summary>
    public string Type { get; set; }

    /// <summary>The name of the index the document is in, as the caller names it, without the provider's index prefix.</summary>
    public string Index { get; set; }

    /// <summary>The document's fields. Setting <c>null</c>, in code or by deserializing, gives an empty dictionary.</summary>
    public IDictionary<string, IList<object>> Fields
    {
        get;
        set => field = value ?? new Dictionary<string, IList<object>>();
    } = new Dictionary<string, IList<object>>();
}
