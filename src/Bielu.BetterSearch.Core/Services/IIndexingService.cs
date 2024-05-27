namespace Bielu.BetterSearch.Abstractions.Services;

public class IndexingServiceAsync(IIndexingProviderAsync indexingProvider,IDocumentValidatorAsync documentValidator) : IIndexingServiceAsync
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

    public async Task IndexDocumentAsync(SearchDocument document)
    {
        if (!(await documentValidator.ValidateDocumentAsync(document)))
        {
            //not valid to do it in checks
            //indexing finished
            foreach (var observer in _indexingObservers)
            {
                observer.OnCompleted();
            }
        }
        foreach (var observer in _indexingObservers)
        {
            observer.OnNext(document);
        }

    }

    public Task<int> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document) => throw new NotImplementedException();

    public async Task RemoveDocumentAsync(DeleteDocumentRequest document)
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
    }



    public async Task<int> RemoveAllDocumentsAsync()
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
