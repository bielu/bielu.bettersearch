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
        // Query translation is not implemented yet, so report it rather than return a query that drops the
        // requested predicates, paging and sorting; see FEATURE_MATRIX.md.
        Task.FromResult(Result.Fail<MeilisearchQuery>("Query translation is not supported by the Meilisearch provider yet"));
}
