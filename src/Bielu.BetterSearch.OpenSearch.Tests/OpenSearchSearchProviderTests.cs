using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Tests;
using FluentResults;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using Shouldly;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchSearchProviderTests(
    IClientFactoryAsync<IOpenSearchClient> clientFactory,
    IOptionsMonitor<OpenSearchOptions> options,
    IQueryTranslateServiceAsync<QueryContainer> queryTranslateService,
    IPostFilterTranslateServiceAsync<QueryContainer> postFilterTranslateService,
    IIndexingProviderAsync indexingProvider)
{
    private sealed class RejectingTranslateService : IQueryTranslateServiceAsync<QueryContainer>
    {
        public Task<Result<QueryContainer>> TranslateMainQueryAsync(ISearchQuery query) =>
            Task.FromResult(Result.Fail<QueryContainer>("rejected by the replacement"));
    }

    [Fact]
    public async Task SearchAsync_ReplacedTranslateService_UsesItForTheMainQuery()
    {
        var provider = new OpenSearchSearchProviderAsync(
            new RejectingTranslateService(),
            postFilterTranslateService,
            clientFactory,
            new OpenSearchResultMapper(),
            options,
            new OpenSearchClientSettingsManager(options));

        var result = await provider.SearchAsync(new SearchQuery { Index = "any" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("rejected by the replacement");
    }

    [Fact]
    public async Task SearchAsync_TotalCount_IsExactUpToMaxResultWindow()
    {
        var index = $"total-{Guid.NewGuid():N}";
        foreach (var id in Enumerable.Range(1, 5))
        {
            (await indexingProvider.IndexDocumentAsync(new SearchDocument { Id = id.ToString(), Index = index }, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }

        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index, ct: TestContext.Current.CancellationToken);
        var provider = new OpenSearchSearchProviderAsync(
            queryTranslateService,
            postFilterTranslateService,
            clientFactory,
            new OpenSearchResultMapper(),
            new TestOptionsMonitor<OpenSearchOptions>(new OpenSearchOptions { MaxResultWindow = 3 }),
            new OpenSearchClientSettingsManager(options));

        var result = await provider.SearchAsync(new SearchQuery { Index = index, PageSize = 1 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Pagination.TotalCount.ShouldBe(3);
        result.Value.Pagination.TotalPages.ShouldBe(3);
    }
}
