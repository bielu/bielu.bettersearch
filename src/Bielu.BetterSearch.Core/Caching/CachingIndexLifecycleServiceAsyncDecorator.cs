using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Decorates an <see cref="IIndexLifecycleServiceAsync"/> so that completing a rebuild evicts the cached searches of
/// the rebuilt index (<see cref="SearchCacheOptions.IndexTag"/>) once the decorated service returns, whether it
/// succeeded or not, as an unacknowledged swap can still apply. Starting, filling and aborting a rebuild don't change
/// what searches of the index return, so they don't evict. The eviction ignores the caller's cancellation, and one that
/// throws is logged and doesn't change the <see cref="Result"/>.
/// </summary>
internal sealed class CachingIndexLifecycleServiceAsyncDecorator(
    IIndexLifecycleServiceAsync inner,
    ISearchResultCache cache,
    ILogger<CachingIndexLifecycleServiceAsyncDecorator>? logger = null)
    : IIndexLifecycleServiceAsync
{
    public Task<Result<IndexRebuild>> StartRebuildAsync(string index, CancellationToken cancellationToken = default) =>
        inner.StartRebuildAsync(index, cancellationToken);

    public Task<Result<int>> IndexRebuildDocumentsAsync(IndexRebuild rebuild, IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default) =>
        inner.IndexRebuildDocumentsAsync(rebuild, documents, cancellationToken);

    public async Task<Result<IReadOnlyList<string>>> CompleteRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.CompleteRebuildAsync(rebuild, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(rebuild?.Index).ConfigureAwait(false);
        }
    }

    public Task<Result<bool>> AbortRebuildAsync(IndexRebuild rebuild, CancellationToken cancellationToken = default) =>
        inner.AbortRebuildAsync(rebuild, cancellationToken);

    /// <summary>Evicts the cached searches of <paramref name="index"/>; never throws.</summary>
    private async Task EvictIndexAsync(string? index)
    {
        try
        {
            await cache.RemoveByTagAsync(SearchCacheOptions.IndexTag(index), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger?.LogError(e, "The cached searches of index {Index} were not evicted after a rebuild completed; they stay until they expire.", index);
        }
    }
}
