using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Diagnostics;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Services;

/// <summary>The default <see cref="IIndexingServiceAsync"/>; see it for the observer contract.</summary>
public class IndexingServiceAsync(
    IIndexingProviderAsync indexingProvider,
    IDocumentValidatorAsync documentValidator,
    ILogger<IndexingServiceAsync>? logger = null)
    : IIndexingServiceAsync
{
    private readonly ObserverList<SearchDocument> _indexingObservers = new(logger);
    private readonly ObserverList<DeleteDocumentRequest> _deleteDocumentObservers = new(logger);
    private readonly ObserverList<DeleteAllDocumentsRequest> _deleteAllDocumentsObservers = new(logger);

    public IDisposable Subscribe(IObserver<SearchDocument> observer) => _indexingObservers.Subscribe(observer);

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var result = await BetterSearchTelemetry.RunAsync("index", document?.Index, indexingProvider, logger, null,
            () => IndexDocumentCoreAsync(document!, cancellationToken)).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            BetterSearchTelemetry.RecordIndexed(1, document?.Index, indexingProvider);
        }

        return result;
    }

    private async Task<Result> IndexDocumentCoreAsync(SearchDocument document, CancellationToken cancellationToken)
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

        var result = await indexingProvider.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
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
        var index = SingleIndex(documents.Select(doc => doc?.Index));
        var result = await BetterSearchTelemetry.RunAsync("bulk index", index, indexingProvider, logger,
            activity => activity.SetTag(BetterSearchTelemetry.DocumentCountTag, documents.Count),
            () => IndexMultipleDocumentsCoreAsync(documents, cancellationToken)).ConfigureAwait(false);
        if (result.IsSuccess && result.Value > 0)
        {
            BetterSearchTelemetry.RecordIndexed(result.Value, index, indexingProvider);
        }

        return result;
    }

    /// <summary>
    /// The index every value names, or <c>null</c> when they name none or more than one. Names that differ only in
    /// case count as two, as a provider such as Lifti stores them as two indexes.
    /// </summary>
    private static string? SingleIndex(IEnumerable<string?> indexes)
    {
        var distinct = indexes.Distinct(StringComparer.Ordinal).Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private async Task<Result<int>> IndexMultipleDocumentsCoreAsync(List<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        // Reject the whole batch before indexing any of it, so a null field can't leave it partly stored.
        var nullFieldsResult = Result.Merge(documents.Select(CheckNullFields).ToArray());
        if (nullFieldsResult.IsFailed)
        {
            return Result.Fail<int>(nullFieldsResult.Errors);
        }

        if (documents.Count == 0)
        {
            return Result.Ok(0);
        }

        // Validate every document before sending any, so a rejected document can't leave the batch partly stored.
        var errors = new List<IError>();
        foreach (var doc in documents)
        {
            errors.AddRange((await ValidateAsync(doc, cancellationToken).ConfigureAwait(false)).Errors);
        }

        if (errors.Count > 0)
        {
            return Result.Fail<int>(errors);
        }

        // One bulk call. When it fails, observers get nothing, even for documents the engine already stored.
        var result = await indexingProvider.IndexMultipleDocumentsAsync(documents, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            foreach (var doc in documents)
            {
                _indexingObservers.Notify(doc);
            }
        }

        return result;
    }

    /// <summary>
    /// Runs the validator on a document that <see cref="CheckNullFields"/> has already passed. The error names the
    /// document and holds its id in the <c>id</c> metadata.
    /// </summary>
    private async Task<Result> ValidateAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        var validationResult = await documentValidator.ValidateDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        if (validationResult.IsFailed)
        {
            return Result.Fail(new Error($"Validation of document {document.Id} failed.").WithMetadata("id", document.Id).CausedBy(validationResult.Errors));
        }

        if (validationResult.Value == false)
        {
            return Result.Fail(new Error($"Document {document.Id} is not valid.").WithMetadata("id", document.Id));
        }

        return Result.Ok();
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

    // Not counted in bettersearch.documents.removed: the provider does not say whether the document was there.
    public Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document,
        CancellationToken cancellationToken = default) =>
        BetterSearchTelemetry.RunAsync("remove", document?.Type, indexingProvider, logger, null,
            () => RemoveDocumentCoreAsync(document!, cancellationToken));

    private async Task<Result> RemoveDocumentCoreAsync(DeleteDocumentRequest document,
        CancellationToken cancellationToken)
    {
        var result = await indexingProvider.RemoveDocumentAsync(document.Id, document.Type, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _deleteDocumentObservers.Notify(document);
        }

        return result;
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var result = await BetterSearchTelemetry.RunAsync("bulk remove", index, indexingProvider, logger, null,
            () => RemoveDocumentsCoreAsync(index, ids, cancellationToken),
            (activity, removed) => activity.SetTag(BetterSearchTelemetry.DocumentCountTag, removed.Value)).ConfigureAwait(false);
        if (result.IsSuccess && result.Value > 0)
        {
            BetterSearchTelemetry.RecordRemoved(result.Value, index, indexingProvider);
        }

        return result;
    }

    private async Task<Result<int>> RemoveDocumentsCoreAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken)
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

        var result = await indexingProvider.RemoveDocumentsAsync(index, distinctIds.Value, cancellationToken).ConfigureAwait(false);
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
        var result = await BetterSearchTelemetry.RunAsync("remove all", request?.IndexName, indexingProvider, logger, null,
            () => RemoveAllDocumentsCoreAsync(request!, cancellationToken),
            (activity, removed) => activity.SetTag(BetterSearchTelemetry.DocumentCountTag, removed.Value)).ConfigureAwait(false);
        if (result.IsSuccess && result.Value > 0)
        {
            BetterSearchTelemetry.RecordRemoved(result.Value.Value, request?.IndexName, indexingProvider);
        }

        return result;
    }

    private async Task<Result<long?>> RemoveAllDocumentsCoreAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken)
    {
        var result = await indexingProvider.RemoveAllDocumentsAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _deleteAllDocumentsObservers.Notify(request);
        }

        return result;
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
