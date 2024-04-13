namespace Bielu.BetterSearch.Abstractions.Services;

public class IndexingService  : IIndexingService
{
    List<IObserver<SearchDocument>> _observers = new();
    public IDisposable Subscribe(IObserver<SearchDocument> observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);

        return new Unsubscriber<SearchDocument>(_observers, observer);
    }
}
