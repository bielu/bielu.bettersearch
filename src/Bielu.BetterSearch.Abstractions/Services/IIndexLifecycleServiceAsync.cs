using Bielu.BetterSearch.Abstractions.Models;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>
/// Rebuilds an index without downtime through the registered provider's <see cref="IIndexLifecycleProviderAsync"/>;
/// see it for the contract. Every method returns a failed <see cref="Result"/> when the provider does not implement it.
/// </summary>
public interface IIndexLifecycleServiceAsync
{
    /// <inheritdoc cref="IIndexLifecycleProviderAsync.StartRebuildAsync"/>
    Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks <paramref name="documents"/> as <see cref="IIndexingServiceAsync.IndexMultipleDocumentsAsync"/> does,
    /// rejecting the whole batch when one fails, and indexes them into the rebuild's new index.
    /// </summary>
    Task<Result<int>> IndexRebuildDocumentsAsync(IndexRebuild rebuild, IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIndexLifecycleProviderAsync.CompleteRebuildAsync"/>
    Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIndexLifecycleProviderAsync.AbortRebuildAsync"/>
    Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default);
}
