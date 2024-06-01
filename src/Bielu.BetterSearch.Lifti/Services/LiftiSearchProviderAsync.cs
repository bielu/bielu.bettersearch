using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiSearchProviderAsync(
    IQueryTranslateServiceAsync<IQuery> queryTranslateServiceAsync,
    ILiftiIndexManager manager,
    IResultMapper<ISearchResults<string>> resultMapper) : ISearchProviderAsync
{
    public async Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchModel> query)
    {
        var index = manager.GetOrCreateIndexAsync(query.Index);
        var translateMainQuery = await queryTranslateServiceAsync.TranslateMainQuery(query);
        if (translateMainQuery.IsFailed)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(translateMainQuery.Errors);
        }

        var mainResult = index.Search(translateMainQuery.Value);
        var result = resultMapper.MapResult(mainResult);
        return Result.Ok(result);
    }
}
