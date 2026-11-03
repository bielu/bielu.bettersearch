using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchQueryTranslateServiceTests(IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>> translateService)
{
    [Fact]
    public async Task TranslateMainQueryAsync_SearchQuery_ReturnsBoolQuery()
    {
        var result = await translateService.TranslateMainQueryAsync(new SearchQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }
}
