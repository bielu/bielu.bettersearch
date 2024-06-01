using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyIndexingServiceAsync : IIndexingServiceAsync
{
    public IDisposable Subscribe(IObserver<SearchDocument> observer) => throw new NotImplementedException();

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer) => throw new NotImplementedException();

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer) => throw new NotImplementedException();

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<int>> RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
