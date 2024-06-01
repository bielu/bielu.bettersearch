using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummyIndexingProviderAsync : IIndexingProviderAsync
{
    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<int>> RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> DeleteIndexAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> IndexExistsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
