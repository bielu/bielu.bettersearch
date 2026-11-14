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

        var validationResult = await ValidateAsync(document, cancellationToken).ConfigureAwait(false);
        if (validationResult.IsFailed)
        {
            return validationResult;
        }

        Result result;
        try
        {
            result = await indexingProvider.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await EvictIndexAsync(document.Index).ConfigureAwait(false);
        }

        if (result.IsSuccess)
        {
            _indexingObservers.Notify(document);
        }

        return result;
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

        // Evicts each index once, after the batch rather than once for each document, and only then notifies the
        // observers of the documents that were indexed, so an observer that searches doesn't read the evicted results.
        var written = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
        var indexed = new List<SearchDocument>();
        try
        {
            foreach (var doc in documents)
            {
                var validationResult = await ValidateAsync(doc, cancellationToken).ConfigureAwait(false);
                if (validationResult.IsFailed)
                {
                    return Result.Fail<int>(validationResult.Errors);
                }

                written.Add(doc.Index);
                var result = await indexingProvider.IndexDocumentAsync(doc, cancellationToken).ConfigureAwait(false);
                if (result.IsFailed)
                {
                    return Result.Fail<int>(result.Errors);
                }

                indexed.Add(doc);
            }
        }
        finally
        {
            foreach (var index in written)
            {
                await EvictIndexAsync(index).ConfigureAwait(false);
            }

            foreach (var doc in indexed)
            {
                _indexingObservers.Notify(doc);
            }
        }

        return Result.Ok(documents.Count);
    }

    /// <summary>Validates a document that <see cref="CheckNullFields"/> has already passed.</summary>
    private async Task<Result> ValidateAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        var validationResult = await documentValidator.ValidateDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        if (validationResult.IsFailed)
        {
            return Result.Fail("Validation failed");
        }

        return validationResult.Value ? Result.Ok() : Result.Fail("Document is not valid");
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
