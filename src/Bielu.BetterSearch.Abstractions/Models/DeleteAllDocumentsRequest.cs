namespace Bielu.BetterSearch.Abstractions.Models;

/// <summary>A request to remove every document from one index.</summary>
public class DeleteAllDocumentsRequest
{
    /// <summary>The name of the index to remove every document from.</summary>
    public required string IndexName { get; set; }
}
