using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Services;

public class IndexingServiceAsync(IIndexingProviderAsync indexingProvider, IDocumentValidatorAsync documentValidator)
    : IIndexingServiceAsync
{
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<IObserver<SearchDocument>> _indexingObservers = new();

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<IObserver<DeleteDocumentRequest>> _deleteDocumentObservers = new();

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<IObserver<DeleteAllDocumentsRequest>> _deleteAllDocumentsObservers = new();


    public IDisposable Subscribe(IObserver<SearchDocument> observer)
    {
        if (!_indexingObservers.Contains(observer))
        {
            _indexingObservers.Add(observer);
        }

        return new Unsubscriber<SearchDocument>(_indexingObservers, observer);
    }

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var nullFieldsResult = CheckNullFields(document);
        if (nullFieldsResult.IsFailed)
        {
            CompleteIndexingObservers();
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
            CompleteIndexingObservers();
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
            //not valid to do it in checks
            //indexing finished
            CompleteIndexingObservers();
            return Result.Fail("Document is not valid");
        }

        foreach (var observer in _indexingObservers)
        {
            observer.OnNext(document);
        }

        return await indexingProvider.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private void CompleteIndexingObservers()
    {
        foreach (var observer in _indexingObservers)
        {
            observer.OnCompleted();
        }
    }

    /// <summary>Fails when the document, its fields, a field or a value in a field is <c>null</c>.</summary>
    private static Result CheckNullFields(SearchDocument document)
    {
        if (document is null)
        {
            return Result.Fail("A document is null.");
        }

        if (document.Fields is null)
        {
            return Result.Fail($"Document {document.Id} has null fields; use an empty dictionary for a document with no fields.");
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
        foreach (var observer in _deleteDocumentObservers)
        {
            observer.OnNext(document);
        }

        var result = await indexingProvider.RemoveDocumentAsync(document.Id, document.Type, cancellationToken).ConfigureAwait(false);
        //indexing finished
        foreach (var observer in _deleteDocumentObservers)
        {
            observer.OnCompleted();
        }

        return result;
    }


    public async Task<Result<int>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var observer in _deleteAllDocumentsObservers)
        {
            observer.OnNext(request);
        }

        var result = await indexingProvider.RemoveAllDocumentsAsync(request, cancellationToken).ConfigureAwait(false);
        //indexing finished
        foreach (var observer in _deleteAllDocumentsObservers)
        {
            observer.OnCompleted();
        }

        return result.ToResult();
    }

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer)
    {
        if (!_deleteDocumentObservers.Contains(observer))
        {
            _deleteDocumentObservers.Add(observer);
        }

        return new Unsubscriber<DeleteDocumentRequest>(_deleteDocumentObservers, observer);
    }

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer)
    {
        if (!_deleteAllDocumentsObservers.Contains(observer))
        {
            _deleteAllDocumentsObservers.Add(observer);
        }

        return new Unsubscriber<DeleteAllDocumentsRequest>(_deleteAllDocumentsObservers, observer);
    }
}
