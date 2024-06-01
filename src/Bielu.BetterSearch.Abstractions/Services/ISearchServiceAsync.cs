using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

public interface ISearchServiceAsync : IObservable<ISearchModel>
{
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchModel> query);
}
