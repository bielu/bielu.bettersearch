using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingServiceAsync : IObservable<SearchDocument>,IObservable<DeleteDocumentRequest>,IObservable<DeleteAllDocumentsRequest>
{
    Task<Result> IndexDocumentAsync(SearchDocument document);
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document);
    Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document);
    Task<Result<int>> RemoveAllDocumentsAsync();
}
