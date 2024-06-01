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

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default)
    {
        foreach (var observer in _deleteDocumentObservers)
        {
            observer.OnNext(document);
        }

        //not valid to do it in checks
        //indexing finished
        foreach (var observer in _deleteDocumentObservers)
        {
            observer.OnCompleted();
        }

        return Result.Ok();
    }


    public async Task<Result<int>> RemoveAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var deleteAllDocumentsRequest = new DeleteAllDocumentsRequest();
        foreach (var observer in _deleteAllDocumentsObservers)
        {
            observer.OnNext(deleteAllDocumentsRequest);
        }

        //not valid to do it in checks
        //indexing finished
        foreach (var observer in _deleteAllDocumentsObservers)
        {
            observer.OnCompleted();
        }

        return 0;
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
