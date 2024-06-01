using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummySearchServiceAsync : ISearchServiceAsync
{


    public IDisposable Subscribe(IObserver<ISearchModel> observer) => throw new NotImplementedException();
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchModel> query) => throw new NotImplementedException();

}
