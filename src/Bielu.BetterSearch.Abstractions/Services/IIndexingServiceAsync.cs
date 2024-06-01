using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingServiceAsync : IObservable<SearchDocument>,IObservable<DeleteDocumentRequest>,IObservable<DeleteAllDocumentsRequest>
{
    Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default);
    Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default);
    Task<Result<int>> RemoveAllDocumentsAsync(CancellationToken cancellationToken = default);
}
