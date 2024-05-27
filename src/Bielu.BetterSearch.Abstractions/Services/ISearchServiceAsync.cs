using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface ISearchServiceAsync : IObservable<ISearchModel>
{
    public Task<ISearchResult<ISearchModel>> SearchAsync(string query);
}
