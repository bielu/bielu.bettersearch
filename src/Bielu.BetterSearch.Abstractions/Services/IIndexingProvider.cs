namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingProviderAsync
{
    Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    Task IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default);
    Task RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default);
    Task RemoveAllDocumentsAsync(CancellationToken cancellationToken = default);
}
