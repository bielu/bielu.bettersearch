using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Services;

/// <summary>The default <see cref="IIndexingServiceAsync"/>; see it for the observer contract.</summary>
/// <remarks>
/// With an <see cref="ISearchResultCache"/>, every write that reaches the provider evicts the cached searches of its
/// index (<see cref="SearchCacheOptions.IndexTag"/>) once the provider returns, whether the write succeeded or not, as a
/// failed write can still have changed part of the index, and before the observers are notified. The eviction ignores
/// the caller's cancellation, so a write that went through always evicts. An eviction that throws is logged and does
/// not change the write's <see cref="Result"/>.
/// </remarks>
public class IndexingServiceAsync(
    IIndexingProviderAsync indexingProvider,
    IDocumentValidatorAsync documentValidator,
    ILogger<IndexingServiceAsync>? logger = null,
    ISearchResultCache? cache = null)
    : IIndexingServiceAsync
{
    private readonly ObserverList<SearchDocument> _indexingObservers = new(logger);
    private readonly ObserverList<DeleteDocumentRequest> _deleteDocumentObservers = new(logger);
    private readonly ObserverList<DeleteAllDocumentsRequest> _deleteAllDocumentsObservers = new(logger);

    public IDisposable Subscribe(IObserver<SearchDocument> observer) => _indexingObservers.Subscribe(observer);

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var nullFieldsResult = CheckNullFields(document);
        if (nullFieldsResult.IsFailed)
        {
            return nullFieldsResult;
        }

        return await IndexCheckedDocumentAsync(document, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document,
        CancellationToken cancellationToken = default)
    {
        var documents = document.ToList();
        // Reject the whole batch before indexing any of it, so a null field can't leave it partly stored.
        var nullFieldsResult = Result.Merge(documents.Select(CheckNullFields).ToArray());
        if (nullFieldsResult.IsFailed)
        {
            return Result.Fail<int>(nullFieldsResult.Errors);
        }

        // Evicts each index once, after the batch, rather than once for each document.
        var written = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var doc in documents)
            {
                written.Add(doc.Index);
                var result = await IndexCheckedDocumentAsync(doc, false, cancellationToken).ConfigureAwait(false);
                if (result.IsFailed)
                {
                    return Result.Fail<int>(result.Errors);
                }
            }
        }
        finally
        {
            foreach (var index in written)
            {
                await EvictIndexAsync(index).ConfigureAwait(false);
            }
        }

        return Result.Ok(documents.Count);
    }

    /// <summary>
    /// Validates and indexes a document that <see cref="CheckNullFields"/> has already passed, evicting its index's
    /// cached searches when <paramref name="evict"/> is <c>true</c>.
    /// </summary>
    private async Task<Result> IndexCheckedDocumentAsync(SearchDocument document, bool evict, CancellationToken cancellationToken)
    {
        var validationResult = await documentValidator.ValidateDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        if (validationResult.IsFailed)
        {
            return Result.Fail("Validation failed");
        }

        if (validationResult.Value == false)
        {
            return Result.Fail("Document is not valid");
        }

        Result result;
        try
        {
            result = await indexingProvider.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (evict)
            {
                await EvictIndexAsync(document.Index).ConfigureAwait(false);
            }
        }

        if (result.IsSuccess)
        {
            _indexingObservers.Notify(document);
        }

        return result;
    }

    /// <summary>
    /// Fails when the document, a field or a value in a field is <c>null</c>. <c>null</c> fields can't reach here,
    /// as <see cref="SearchDocument.Fields"/> turns them into an empty dictionary.
    /// </summary>
    private static Result CheckNullFields(SearchDocument document)
    {
        if (document is null)
        {
            return Result.Fail("A document is null.");
        }

        var nullFields = new List<string>();
        var nullValueFields = new List<string>();
        foreach (var (name, values) in document.Fields)
        {
            if (values == null)
            {
                nullFields.Add(name);
            }
            else if (values.Any(value => value is null))
            {
                nullValueFields.Add(name);
            }
        }

        if (nullFields.Count > 0)
        {
            return Result.Fail($"Document {document.Id} has null fields ({string.Join(", ", nullFields)}); use an empty list for a field with no values.");
        }

        if (nullValueFields.Count > 0)
        {
            return Result.Fail($"Document {document.Id} has null values in fields ({string.Join(", ", nullValueFields)}); leave them out of the list.");
        }

        return Result.Ok();
    }

    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document,
        CancellationToken cancellationToken = default)
    {
        Result result;
        try
        {
            result = await indexingProvider.RemoveDocumentAsync(document.Id, document.Type, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(document.Type).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            _deleteDocumentObservers.Notify(document);
        }

        return result;
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed)
        {
            return distinctIds.ToResult<int>();
        }

        if (distinctIds.Value.Count == 0)
        {
            return Result.Ok(0);
        }

        Result<int> result;
        try
        {
            result = await indexingProvider.RemoveDocumentsAsync(index, distinctIds.Value, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(index).ConfigureAwait(false);
        }

        if (result.IsSuccess)
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
            result = await indexingProvider.RemoveAllDocumentsAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(request?.IndexName).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            _deleteAllDocumentsObservers.Notify(request);
        }

        return result;
    }

    /// <summary>Evicts the cached searches of <paramref name="index"/>; never throws.</summary>
    private async Task EvictIndexAsync(string? index)
    {
        if (cache is null)
        {
            return;
        }

        try
        {
            await cache.RemoveByTagAsync(SearchCacheOptions.IndexTag(index), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger?.LogError(e, "The cached searches of index {Index} were not evicted after a write; they stay until they expire.", index);
        }
    }

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer) => _deleteDocumentObservers.Subscribe(observer);

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer) => _deleteAllDocumentsObservers.Subscribe(observer);

    /// <summary>Ends every stream: each observer gets <see cref="IObserver{T}.OnCompleted"/> once and is unsubscribed.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Ends every stream when <paramref name="disposing"/> is <c>true</c>. Never throws, and later calls do nothing.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _indexingObservers.Complete();
            _deleteDocumentObservers.Complete();
            _deleteAllDocumentsObservers.Complete();
        }
    }
}
