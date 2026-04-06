using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services.NullImplementations;

public class DummySearchProviderAsync : ISearchProviderAsync
{
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchResult<ISearchModel>> query) => throw new NotImplementedException();
}
