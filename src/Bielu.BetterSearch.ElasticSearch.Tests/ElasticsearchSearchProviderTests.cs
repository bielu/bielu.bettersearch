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
    IOptionsMonitor<ElasticSearchOptions> options)
{
    private sealed class RejectingTranslateService : IQueryTranslateServiceAsync<EsQuery>
    {
        public Task<Result<EsQuery>> TranslateMainQueryAsync(ISearchQuery<ISearchResult<ISearchModel>> query) =>
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
            options);

        var result = await provider.SearchAsync(new SearchQuery { Index = "any" });

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
}
