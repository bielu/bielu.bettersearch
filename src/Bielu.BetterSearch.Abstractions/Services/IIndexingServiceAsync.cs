using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>
/// Indexes and removes documents. To run code after a write, decorate this service in DI, as the search cache does.
/// </summary>
public interface IIndexingServiceAsync
{
    Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    /// <summary>
    /// Checks and validates every document, then indexes them all with one call to
    /// <see cref="IIndexingProviderAsync.IndexMultipleDocumentsAsync"/> and returns how many were indexed. A document
    /// that is <c>null</c>, has a <c>null</c> or empty <c>Id</c> or <c>Index</c>, has a <c>null</c> field or value,
    /// or that the validator rejects fails the batch before anything is sent, so none of it is stored. A document the engine rejects fails the batch too; what the engine
    /// stored of the rest depends on the provider, and the error names the ids that were not stored in its
    /// <c>ids</c> metadata.
    /// </summary>
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default);
    Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes the documents with <paramref name="ids"/> from <paramref name="index"/> and returns how many were
    /// removed; see <see cref="IIndexingProviderAsync.RemoveDocumentsAsync"/>.
    /// </summary>
    Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes every document from the index named by <paramref name="request"/> and returns how many were removed,
    /// as reported by <see cref="IIndexingProviderAsync.RemoveAllDocumentsAsync"/>. A <c>null</c> value means the
    /// engine removed the documents but did not report how many.
    /// </summary>
    Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces some fields of one document; see <see cref="IIndexingProviderAsync.UpdateDocumentAsync"/>. An empty
    /// update, or one with a <c>null</c> field or value, fails before the provider is called. The document validator
    /// is not run, as it checks whole documents.
    /// </summary>
    Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Fail<bool>($"{GetType().Name} does not support UpdateDocumentAsync."));
}
