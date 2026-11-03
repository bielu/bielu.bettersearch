using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchQueryTranslateServiceTests(IQueryTranslateServiceAsync<MeilisearchQuery> translateService)
{
    [Fact]
    public async Task TranslateMainQueryAsync_SearchQuery_ReturnsFailure()
    {
        var result = await translateService.TranslateMainQueryAsync(new SearchQuery());

        result.IsFailed.ShouldBeTrue();
    }
}
