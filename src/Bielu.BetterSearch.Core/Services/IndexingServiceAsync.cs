using Bielu.BetterSearch.Abstractions.Services;
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
        var documentResult = CheckDocument(document);
        if (documentResult.IsFailed)
        {
            return documentResult;
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
        if (document is null)
        {
            return Result.Fail<int>("The documents to index are null; pass an empty list to index nothing.");
        }

        var documents = document.ToList();
        // Reject the whole batch before indexing any of it, so a null field can't leave it partly stored.
        var documentsResult = Result.Merge(documents.Select(CheckDocument).ToArray());
        if (documentsResult.IsFailed)
        {
            return Result.Fail<int>(documentsResult.Errors);
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
    /// Runs the validator on a document that <see cref="CheckDocument"/> has already passed. The error names the
    /// document and holds its id in the <c>id</c> metadata. A validator that throws fails the same way, with the
    /// exception as the cause; cancellation still propagates.
    /// </summary>
    private async Task<Result> ValidateAsync(SearchDocument document, CancellationToken cancellationToken)
    {
        Result<bool> validationResult;
        try
        {
            validationResult = await documentValidator.ValidateDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error($"Validation of document {document.Id} failed: the validator threw {e.GetType().Name}.").WithMetadata("id", document.Id).CausedBy(e));
        }

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
    /// Fails when the document, a field or a value in a field is <c>null</c>, or when its <c>Id</c> or <c>Index</c>
    /// is <c>null</c> or empty. <c>null</c> fields can't reach here, as <see cref="SearchDocument.Fields"/> turns them
    /// into an empty dictionary.
    /// </summary>
    private static Result CheckDocument(SearchDocument document)
    {
        if (document is null)
        {
            return Result.Fail("A document is null.");
        }

        if (string.IsNullOrEmpty(document.Id))
        {
            return Result.Fail($"A document for index '{document.Index}' has a null or empty Id.");
        }

        if (string.IsNullOrEmpty(document.Index))
        {
            return Result.Fail($"Document {document.Id} has a null or empty Index.");
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
        if (document is null)
        {
            return Result.Fail("The request to remove a document is null.");
        }

        if (string.IsNullOrEmpty(document.Id))
        {
            return Result.Fail($"The request to remove a document from '{document.Type}' has a null or empty Id.");
        }

        if (string.IsNullOrEmpty(document.Type))
        {
            return Result.Fail($"The request to remove document {document.Id} has a null or empty Type, the name of the index.");
        }

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
        if (request is null)
        {
            return Result.Fail<long?>("The request to remove all documents is null.");
        }

        if (string.IsNullOrEmpty(request.IndexName))
        {
            return Result.Fail<long?>("The request to remove all documents has a null or empty IndexName.");
        }

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
