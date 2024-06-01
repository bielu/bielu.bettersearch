using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiSearchProviderAsync : ISearchProviderAsync
{
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchModel> query) => throw new NotImplementedException();
}
