using Bielu.BetterSearch.Abstractions.Caching;
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
/// The decorator has its own observers: <c>Subscribe</c> registers on it, and it notifies them after the eviction,
/// following the <see cref="IIndexingServiceAsync"/> contract, so an observer that searches the index reads past the
/// evicted result. The decorated service's own observers are not used through it. The eviction ignores the caller's
/// cancellation, so a write that went through always evicts. An eviction that throws is logged and does not change the
/// write's <see cref="Result"/>. Disposing the decorator completes its observers only; the container disposes the
/// decorated service.
/// </remarks>
internal sealed class CachingIndexingServiceAsyncDecorator(
    IIndexingServiceAsync inner,
    ISearchResultCache cache,
    ILogger<CachingIndexingServiceAsyncDecorator>? logger = null)
    : IIndexingServiceAsync
{
    private readonly ObserverList<SearchDocument> _indexingObservers = new(logger);
    private readonly ObserverList<DeleteDocumentRequest> _deleteDocumentObservers = new(logger);
    private readonly ObserverList<DeleteAllDocumentsRequest> _deleteAllDocumentsObservers = new(logger);

    public IDisposable Subscribe(IObserver<SearchDocument> observer) => _indexingObservers.Subscribe(observer);

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer) => _deleteDocumentObservers.Subscribe(observer);

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer) => _deleteAllDocumentsObservers.Subscribe(observer);

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        Result result;
        try
        {
            result = await inner.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(document?.Index).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            _indexingObservers.Notify(document!);
        }

        return result;
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default)
    {
        var documents = document?.ToList();
        Result<int> result;
        try
        {
            result = await inner.IndexMultipleDocumentsAsync(documents!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var index in (documents ?? []).Select(doc => doc?.Index).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await EvictIndexAsync(index).ConfigureAwait(false);
            }
        }

        if (result.IsSuccess)
        {
            foreach (var doc in documents ?? [])
            {
                _indexingObservers.Notify(doc);
            }
        }

        return result;
    }

    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default)
    {
        Result result;
        try
        {
            result = await inner.RemoveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(document?.Type).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            _deleteDocumentObservers.Notify(document!);
        }

        return result;
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids?.ToList();
        Result<int> result;
        try
        {
            result = await inner.RemoveDocumentsAsync(index, idList!, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index).ConfigureAwait(false);
        }

        // One notification for each distinct id, as the service contract says.
        if (result.IsSuccess && DocumentIds.Distinct(idList) is { IsSuccess: true } distinctIds)
        {
            foreach (var id in distinctIds.Value)
            {
                _deleteDocumentObservers.Notify(new DeleteDocumentRequest { Id = id, Type = index });
            }
        }

        return result;
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        Result<long?> result;
        try
        {
            result = await inner.RemoveAllDocumentsAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(request?.IndexName).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            _deleteAllDocumentsObservers.Notify(request!);
        }

        return result;
    }

    /// <summary>
    /// Evicts the cached searches of <paramref name="index"/> once the update returns, as the other writes do. Observers
    /// are not notified, as <see cref="IIndexingServiceAsync.UpdateDocumentAsync"/> says.
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

    /// <summary>Ends the streams of this decorator's observers; the decorated service is disposed by its owner.</summary>
    public void Dispose()
    {
        _indexingObservers.Complete();
        _deleteDocumentObservers.Complete();
        _deleteAllDocumentsObservers.Complete();
    }
}
