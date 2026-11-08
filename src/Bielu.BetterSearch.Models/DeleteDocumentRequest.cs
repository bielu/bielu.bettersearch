namespace Bielu.BetterSearch;

public class DeleteDocumentRequest
{
    public string Id { get; set; }
    /// <summary>
    /// The name of the index to remove the document from. Despite its name, this is not the document type held in
    /// <see cref="SearchDocument.Type"/>.
    /// </summary>
    public string Type { get; set; }
}
