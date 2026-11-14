using System.Text;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>
/// 404 answers to <see cref="ISearchProviderAsync.GetDocumentAsync"/>, served from an in-memory request invoker.
/// </summary>
public sealed class ElasticsearchGetDocumentResponseTests
{
    private sealed class FixedClientFactory(ElasticsearchClient client) : IClientFactoryAsync<ElasticsearchClient>
    {
        public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private static ElasticSearchSearchProviderAsync Provider(string body, string contentType = "application/json")
    {
        var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(body), 404, null, contentType,
            new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = ["Elasticsearch"] });
        var client = new ElasticsearchClient(new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:9200")), invoker));
        var options = new TestOptionsMonitor<ElasticSearchOptions>(new ElasticSearchOptions { SharedIndexSettings = new() });
        var translateService = new ElasticSearchQueryTranslateService([]);
        return new ElasticSearchSearchProviderAsync(translateService, translateService, new FixedClientFactory(client),
            new ElasticSearchResultMapper(), options, new ElasticSearchClientSettingsManager(options));
    }

    [Fact]
    public async Task GetDocumentAsync_DocumentNotFound_ReturnsNull()
    {
        var provider = Provider("""{"_index":"people","_id":"1","found":false}""");

        var result = await provider.GetDocumentAsync("people", "1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task GetDocumentAsync_IndexNotFound_ReturnsNull()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [people]","index":"people"}],"type":"index_not_found_exception","reason":"no such index [people]","index":"people"},"status":404}""");

        var result = await provider.GetDocumentAsync("people", "1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("{}", "application/json")]
    [InlineData("<html><body>Not Found</body></html>", "text/html")]
    public async Task GetDocumentAsync_NotFoundWithoutAnEngineAnswer_ReturnsFailure(string body, string contentType)
    {
        var provider = Provider(body, contentType);

        var result = await provider.GetDocumentAsync("people", "1", TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("404");
    }
}
