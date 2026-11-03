using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using MeilisearchQuery = Meilisearch.SearchQuery;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Meilisearch.Services;

internal sealed class MeilisearchQueryTranslateService : IQueryTranslateServiceAsync<MeilisearchQuery>
{
    public Task<Result<MeilisearchQuery>> TranslateMainQueryAsync(ISearchQuery<ISearchResult<ISearchModel>> query) =>
        Task.FromResult(query switch
        {
            SearchQuery => Result.Ok(new MeilisearchQuery()),
            _ => Result.Fail<MeilisearchQuery>("Query type not supported")
        });
}
