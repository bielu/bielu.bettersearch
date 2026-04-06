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
        var validationResult = await documentValidator.ValidateDocumentAsync(document);
        if (validationResult.IsFailed)
        {
            return Result.Fail("Validation failed");
        }

        if (validationResult.Value == false)
        {
            //not valid to do it in checks
            //indexing finished
            foreach (var observer in _indexingObservers)
            {
                observer.OnCompleted();
            }

            return Result.Fail("Document is not valid");
        }

        foreach (var observer in _indexingObservers)
        {
            observer.OnNext(document);
        }

        return await indexingProvider.IndexDocumentAsync(document, cancellationToken);
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document,
        CancellationToken cancellationToken = default)
    {
        var documents = document.ToList();
        foreach (var doc in documents)
        {
            var result = await IndexDocumentAsync(doc, cancellationToken);
            if (result.IsFailed)
            {
                return Result.Fail<int>(result.Errors);
            }
        }
        return Result.Ok(documents.Count);
    }

    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document,
        CancellationToken cancellationToken = default)
    {
        foreach (var observer in _deleteDocumentObservers)
        {
            observer.OnNext(document);
        }

        var result = await indexingProvider.RemoveDocumentAsync(document.Id, document.Type, cancellationToken);
        //indexing finished
        foreach (var observer in _deleteDocumentObservers)
        {
            observer.OnCompleted();
        }

        return result;
    }


    public async Task<Result<int>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request,CancellationToken cancellationToken = default)
    {
        foreach (var observer in _deleteAllDocumentsObservers)
        {
            observer.OnNext(request);
        }

       var result = await indexingProvider.RemoveAllDocumentsAsync(request,cancellationToken);
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
