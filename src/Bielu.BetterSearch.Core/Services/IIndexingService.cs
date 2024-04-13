namespace Bielu.BetterSearch.Abstractions.Services;

public class IndexingService(IIndexingProvider indexingProvider,IDocumentValidator documentValidator) : IIndexingService
{
    List<IObserver<SearchDocument>> _indexingObservers = new();
    List<IObserver<DeleteDocumentRequest>> _deleteDocumentObservers = new();
    List<IObserver<DeleteAllDocumentsRequest>> _deleteAllDocumentsObservers = new();


    public IDisposable Subscribe(IObserver<SearchDocument> observer)
    {
        if (!_indexingObservers.Contains(observer))
            _indexingObservers.Add(observer);

        return new Unsubscriber<SearchDocument>(_indexingObservers, observer);
    }

    public void IndexDocument(SearchDocument document)
    {
        if (!documentValidator.ValidateDocument(document))
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

    public void IndexMultipleDocuments(IEnumerable<SearchDocument> document) => throw new NotImplementedException();

    public void RemoveDocument(DeleteDocumentRequest document)
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



    public void RemoveAllDocuments()
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
    }

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer)
    {
        if (!_deleteDocumentObservers.Contains(observer))
            _deleteDocumentObservers.Add(observer);

        return new Unsubscriber<DeleteDocumentRequest>(_deleteDocumentObservers, observer);
    }

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer)
    {
        if (!_deleteAllDocumentsObservers.Contains(observer))
            _deleteAllDocumentsObservers.Add(observer);

        return new Unsubscriber<DeleteAllDocumentsRequest>(_deleteAllDocumentsObservers, observer);
    }
}
