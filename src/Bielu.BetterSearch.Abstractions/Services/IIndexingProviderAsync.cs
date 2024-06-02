using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingProviderAsync
{
    Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default);
    Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default);
    Task<Result<int>>  RemoveAllDocumentsAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default);
}
