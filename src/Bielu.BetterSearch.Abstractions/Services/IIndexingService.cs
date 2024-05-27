namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingServiceAsync : IObservable<SearchDocument>,IObservable<DeleteDocumentRequest>,IObservable<DeleteAllDocumentsRequest>
{
    Task IndexDocumentAsync(SearchDocument document);
    Task<int> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document);
    Task RemoveDocumentAsync(DeleteDocumentRequest document);
    Task<int> RemoveAllDocumentsAsync();
}
