using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Decorates an <see cref="IIndexingProviderAsync"/> so that every write evicts the cached searches of its index
/// (<see cref="SearchCacheOptions.IndexTag"/>) once the decorated provider returns, whether the write succeeded or not,
/// as a failed write can still have changed part of the index.
/// </summary>
/// <remarks>
/// The writes are indexing, removing and removing all documents, and creating and deleting an index. A batch evicts each
/// of its indexes once. The indexing service sits above this decorator, so the eviction happens before its observers
/// are notified. The eviction ignores the caller's cancellation, so a write that went through always evicts. An eviction
/// that throws is logged and does not change the write's <see cref="Result"/>.
/// </remarks>
internal sealed class CachingIndexingProviderAsyncDecorator(
    IIndexingProviderAsync inner,
    ISearchResultCache cache,
    ILogger<CachingIndexingProviderAsyncDecorator>? logger = null)
    : IIndexingProviderAsync
{
    internal IIndexingProviderAsync Inner => inner;

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(document?.Index).ConfigureAwait(false);
        }
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default)
    {
        var batch = documents?.ToList();
        try
        {
            return await inner.IndexMultipleDocumentsAsync(batch!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var index in (batch ?? []).Select(document => document?.Index).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await EvictIndexAsync(index).ConfigureAwait(false);
            }
        }
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.RemoveDocumentAsync(id, type, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(type).ConfigureAwait(false);
        }
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.RemoveDocumentsAsync(index, ids, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index).ConfigureAwait(false);
        }
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.RemoveAllDocumentsAsync(index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index?.IndexName).ConfigureAwait(false);
        }
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.DeleteIndexAsync(index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index).ConfigureAwait(false);
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index).ConfigureAwait(false);
        }
    }

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        inner.EnsureIndexExistsAsync(index, cancellationToken);

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        inner.IndexExistsAsync(index, cancellationToken);

    /// <summary>Evicts the cached searches of <paramref name="index"/>; never throws.</summary>
    private async Task EvictIndexAsync(string? index)
    {
        try
        {
            await cache.RemoveByTagAsync(SearchCacheOptions.IndexTag(index), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger?.LogError(e, "The cached searches of index {Index} were not evicted after a write; they stay until they expire.", index);
        }
    }
}
