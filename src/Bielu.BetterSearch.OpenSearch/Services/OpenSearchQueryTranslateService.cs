using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchQueryTranslateService : IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>>
{
    public Task<Result<BoolQueryDescriptor<SearchDocument>>> TranslateMainQueryAsync(ISearchQuery<ISearchResult<ISearchModel>> query) =>
        Task.FromResult(query switch
        {
            SearchQuery => Result.Ok(new BoolQueryDescriptor<SearchDocument>()),
            _ => Result.Fail<BoolQueryDescriptor<SearchDocument>>("Query type not supported")
        });
}
