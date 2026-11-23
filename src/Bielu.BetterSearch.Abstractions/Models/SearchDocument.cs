namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>
/// One document, in the one shape used to write it (<c>IndexDocumentAsync</c>), to read it by id
/// (<c>GetDocumentAsync</c>) and to read it from a search hit (<see cref="ISearchHit.Document"/>), so a document read
/// either way can be written back as it is. A hit holds the whole document only when the provider stores fields and the
/// query's <c>Properties</c> don't limit them: on Lifti, which stores no fields, a hit holds only the id and the index,
/// so writing it back replaces the indexed document with an empty one.
/// </summary>
public class SearchDocument
{
    /// <summary>The document id, exactly as it was indexed.</summary>
    public required string Id { get; set; }

    /// <summary>The document's type.</summary>
    public string? Type { get; set; }

    /// <summary>The name of the index the document is in, as the caller names it, without the provider's index prefix.</summary>
    public required string Index { get; set; }

    /// <summary>The document's fields. Setting <c>null</c>, in code or by deserializing, gives an empty dictionary.</summary>
    public IDictionary<string, IList<object>> Fields
    {
        get;
        set => field = value ?? new Dictionary<string, IList<object>>();
    } = new Dictionary<string, IList<object>>();
}
