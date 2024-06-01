using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchSearchProviderAsync : ISearchProviderAsync
{
    public Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchModel> query) => throw new NotImplementedException();
}
