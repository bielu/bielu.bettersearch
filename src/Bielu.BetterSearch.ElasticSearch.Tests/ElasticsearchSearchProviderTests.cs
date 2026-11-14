using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using FluentResults;
using Microsoft.Extensions.Options;
using Shouldly;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchSearchProviderTests(
    IClientFactoryAsync<ElasticsearchClient> clientFactory,
    IOptionsMonitor<ElasticSearchOptions> options,
    IQueryTranslateServiceAsync<EsQuery> queryTranslateService,
    IPostFilterTranslateServiceAsync<EsQuery> postFilterTranslateService,
    IIndexingProviderAsync indexingProvider,
    IElasticSearchClientSettingsManager settingsManager)
{
    private sealed class RejectingTranslateService : IQueryTranslateServiceAsync<EsQuery>
    {
        public Task<Result<EsQuery>> TranslateMainQueryAsync(ISearchQuery query) =>
            Task.FromResult(Result.Fail<EsQuery>("rejected by the replacement"));
    }

    [Fact]
    public async Task SearchAsync_ReplacedTranslateService_UsesItForTheMainQuery()
    {
        var provider = new ElasticSearchSearchProviderAsync(
            new RejectingTranslateService(),
            new ElasticSearchQueryTranslateService([]),
            clientFactory,
            new ElasticSearchResultMapper(),
            options,
            settingsManager);

        var result = await provider.SearchAsync(new SearchQuery { Index = "any" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("rejected by the replacement");
    }

    [Fact]
    public async Task GetOrCreateClientAsync_CalledTwice_ReturnsTheSameClient()
    {
        var first = await clientFactory.GetOrCreateClientAsync("one-index");
        var second = await clientFactory.GetOrCreateClientAsync("another-index");

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task SearchAsync_TotalCount_IsExactUpToMaxResultWindow()
    {
        var index = $"total-{Guid.NewGuid():N}";
        foreach (var id in Enumerable.Range(1, 5))
        {
            (await indexingProvider.IndexDocumentAsync(new SearchDocument { Id = id.ToString(), Index = index }, TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }

        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index, TestContext.Current.CancellationToken);
        var provider = new ElasticSearchSearchProviderAsync(
            queryTranslateService,
            postFilterTranslateService,
            clientFactory,
            new ElasticSearchResultMapper(),
            new FixedOptionsMonitor(new ElasticSearchOptions { MaxResultWindow = 3 }),
            settingsManager);

        var result = await provider.SearchAsync(new SearchQuery { Index = index, PageSize = 1 }, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Pagination.TotalCount.ShouldBe(3);
        result.Value.Pagination.TotalPages.ShouldBe(3);
    }
}
