using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>
/// Rebuilds an index without downtime, for engines with index aliases. An indexing provider implements it alongside
/// <see cref="IIndexingProviderAsync"/> when its engine can; <see cref="IIndexLifecycleServiceAsync"/> fails for a
/// provider that does not.
/// </summary>
/// <remarks>
/// A rebuild creates a new index with the index's configured mappings, fills it, and then points the index name, an
/// alias, at it in one atomic step that also deletes the indexes the alias pointed at before. Until that step,
/// searches and writes through the index name keep using the current index; a write made there during the rebuild is
/// not copied to the new one. When the engine rejects a step, the alias stays where it was. When the engine does not
/// confirm a step in time, the call fails but the step may still apply: call it again to learn the outcome. Completing
/// and aborting the same rebuild exclude each other: once one has taken effect, the other fails.
/// </remarks>
public interface IIndexLifecycleProviderAsync
{
    /// <summary>Creates a new, empty index with the configured mappings of <paramref name="index"/>.</summary>
    Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes <paramref name="documents"/> into the rebuild's new index and returns how many were indexed. Each
    /// document's <see cref="SearchDocument.Index"/> must be the rebuild's <see cref="IndexRebuild.Index"/>.
    /// </summary>
    Task<Result<int>> IndexRebuildDocumentsAsync(IndexRebuild rebuild, IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Points the index name at the rebuild's new index and deletes the indexes it pointed at before, in one atomic
    /// step, and returns the names of the deleted indexes. The first rebuild of an index created without an alias
    /// replaces that index with the alias. Completing a rebuild that is already complete succeeds and deletes nothing.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the rebuild's new index and leaves the alias where it is. <c>false</c> when the new index is already
    /// gone. Fails, deleting nothing, when the rebuild is already complete.
    /// </summary>
    Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default);
}
