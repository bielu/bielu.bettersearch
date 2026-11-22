using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Caching;

/// <summary>
/// Decorates an <see cref="IIndexingServiceAsync"/> so that every write evicts the cached searches of its index
/// (<see cref="SearchCacheOptions.IndexTag"/>) once the decorated service returns, whether the write succeeded or not,
/// as a failed write can still have changed part of the index. A batch evicts each of its indexes once.
/// </summary>
/// <remarks>
/// The eviction ignores the caller's cancellation, so a write that went through always evicts. An eviction that throws
/// is logged and does not change the write's <see cref="Result"/>.
/// </remarks>
internal sealed class CachingIndexingServiceAsyncDecorator(
    IIndexingServiceAsync inner,
    ISearchResultCache cache,
    ILogger<CachingIndexingServiceAsyncDecorator>? logger = null)
    : IIndexingServiceAsync
{
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

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default)
    {
        var documents = document?.ToList();
        try
        {
            return await inner.IndexMultipleDocumentsAsync(documents!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var index in (documents ?? []).Select(doc => doc?.Index).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await EvictIndexAsync(index).ConfigureAwait(false);
            }
        }
    }

    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.RemoveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(document?.Index).ConfigureAwait(false);
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

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.RemoveAllDocumentsAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(request?.IndexName).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Evicts the cached searches of <paramref name="index"/> once the update returns, as the other writes do.
    /// </summary>
    public async Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.UpdateDocumentAsync(index, id, fields, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index).ConfigureAwait(false);
        }
    }

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
