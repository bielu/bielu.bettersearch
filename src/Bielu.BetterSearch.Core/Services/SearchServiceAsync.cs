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

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await providerAsync.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailed)
        {
            return result;
        }

        foreach (var hit in result.Value.Items)
        {
            foreach (var observer in _indexingObservers)
            {
                observer.OnNext(hit.Document);
            }
        }

        return result;
    }
}
