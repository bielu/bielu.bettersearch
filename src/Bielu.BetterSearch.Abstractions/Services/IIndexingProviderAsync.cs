using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface IIndexingProviderAsync
{
    Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default);
    Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes the documents with <paramref name="ids"/> from <paramref name="index"/> in one request where the engine
    /// allows it, and returns how many were removed. An id that is not in the index is not counted and is not an error,
    /// a repeated id is removed once, and an empty list succeeds with 0 without calling the engine. When some ids fail,
    /// the <see cref="Result"/> fails and names them, but the other documents may already be removed.
    /// </summary>
    Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes every document from the index named by <paramref name="index"/> and returns how many were removed, or
    /// <c>null</c> when the engine removed them but did not report how many.
    /// </summary>
    Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default);
    Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default);
    Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default);
}
