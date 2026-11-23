using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>Writes documents and manages indexes on one engine. Implemented by each provider.</summary>
public interface IIndexingProviderAsync
{
    /// <summary>Indexes <paramref name="document"/> into its <see cref="SearchDocument.Index"/>, replacing a document with the same id.</summary>
    /// <param name="document">The document to index.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    /// <summary>Indexes <paramref name="documents"/>, each into its own index, and returns how many were indexed.</summary>
    /// <param name="documents">The documents to index.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default);
    /// <summary>Removes the document with <paramref name="id"/> from <paramref name="index"/>.</summary>
    /// <param name="id">The id of the document to remove.</param>
    /// <param name="index">The index to remove it from.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> RemoveDocumentAsync(string id, string index, CancellationToken cancellationToken = default);
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
    /// <summary>Creates <paramref name="index"/> when it is missing. <c>true</c> when the index exists afterwards.</summary>
    /// <param name="index">The index name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default);
    /// <summary>Deletes <paramref name="index"/>. <c>true</c> when it was deleted.</summary>
    /// <param name="index">The index name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default);
    /// <summary><c>true</c> when <paramref name="index"/> exists, <c>false</c> when the engine says it does not.</summary>
    /// <param name="index">The index name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default);
    /// <summary>Creates <paramref name="index"/>. <c>true</c> when this call created it.</summary>
    /// <param name="index">The index name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the values of each field in <paramref name="fields"/> on the document with <paramref name="id"/> in
    /// <paramref name="index"/>, and keeps its other fields. <c>true</c> when the document was updated; <c>false</c>
    /// when the document, or the whole index, is not there, in which case nothing is created. The update need not be
    /// atomic: a provider that reads and rewrites the whole document (Meilisearch) can overwrite a change another
    /// caller made in between, or store again a document removed in between, and a provider that checks the index
    /// first (Elasticsearch, OpenSearch) lets the engine create again an index deleted between the check and the
    /// update. A provider that does not support it (Lifti) returns a failed <see cref="Result"/> saying so.
    /// </summary>
    Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default);
}
