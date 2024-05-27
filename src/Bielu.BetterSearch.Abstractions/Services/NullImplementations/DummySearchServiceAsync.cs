using Bielu.BetterSearch.Abstractions.Models;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummySearchServiceAsync : ISearchServiceAsync
{


    public IDisposable Subscribe(IObserver<ISearchModel> observer) => throw new NotImplementedException();

    public Task<ISearchResult<ISearchModel>> SearchAsync(string query) => throw new NotImplementedException();
}
