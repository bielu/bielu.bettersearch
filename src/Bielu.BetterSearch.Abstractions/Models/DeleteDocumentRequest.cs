namespace Bielu.BetterSearch.Abstractions.Models;

public class DeleteDocumentRequest
{
    public string Id { get; set; }
    /// <summary>The name of the index to remove the document from.</summary>
    public string Index { get; set; }
}
