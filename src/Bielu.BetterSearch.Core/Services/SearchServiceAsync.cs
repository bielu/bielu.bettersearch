using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Services;

public class SearchServiceAsync(ISearchProviderAsync providerAsync) : ISearchServiceAsync
{
    private List<IObserver<ISearchModel>> _indexingObservers = new();

    public IDisposable Subscribe(IObserver<ISearchModel> observer)
    {
        if (!_indexingObservers.Contains(observer))
        {
            _indexingObservers.Add(observer);
        }

        return new Unsubscriber<ISearchModel>(_indexingObservers, observer);
    }

    public async Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        var result = await providerAsync.SearchAsync(query);
        foreach (var document in result.Value.Items)
        {
            foreach (var observer in _indexingObservers)
            {
                observer.OnNext(document);
            }
        }

        return result;
    }
}
