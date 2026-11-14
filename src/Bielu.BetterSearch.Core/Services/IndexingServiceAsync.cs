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
        var nullFieldsResult = CheckNullFields(document);
        if (nullFieldsResult.IsFailed)
        {
            return nullFieldsResult;
        }

        return await IndexCheckedDocumentAsync(document, cancellationToken).ConfigureAwait(false);
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

        foreach (var doc in documents)
        {
            var result = await IndexCheckedDocumentAsync(doc, cancellationToken).ConfigureAwait(false);
            if (result.IsFailed)
            {
                return Result.Fail<int>(result.Errors);
            }
        }
        return Result.Ok(documents.Count);
    }

    /// <summary>Validates and indexes a document that <see cref="CheckNullFields"/> has already passed.</summary>
    private async Task<Result> IndexCheckedDocumentAsync(SearchDocument document, CancellationToken cancellationToken)
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

        var result = await indexingProvider.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
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
    internal static Result CheckNullFields(SearchDocument document)
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
