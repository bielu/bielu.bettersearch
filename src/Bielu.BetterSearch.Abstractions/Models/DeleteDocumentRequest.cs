namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>A request to remove one document from an index.</summary>
public class DeleteDocumentRequest
{
    /// <summary>The id of the document to remove.</summary>
    public required string Id { get; set; }
    /// <summary>The name of the index to remove the document from.</summary>
    public required string Index { get; set; }
}
