using System.Text;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>
/// Responses a healthy test cluster does not produce, served from an in-memory request invoker.
/// </summary>
public sealed class ElasticsearchIndexingResponseTests
{
    private sealed class FixedClientFactory(ElasticsearchClient client) : IClientFactoryAsync<ElasticsearchClient>
    {
        public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private static ElasticSearchIndexingProviderAsync Provider(string json, int statusCode)
    {
        var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(json), statusCode, null, "application/json",
            new Dictionary<string, IEnumerable<string>> { ["X-Elastic-Product"] = ["Elasticsearch"] });
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:9200")), invoker);
        return new ElasticSearchIndexingProviderAsync(new FixedClientFactory(new ElasticsearchClient(settings)), NullLogger<ElasticSearchIndexingProviderAsync>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemoveDocumentAsync_DocumentNotFound_ReturnsSuccess()
    {
        var provider = Provider("""{"_index":"people","_id":"1","_version":1,"result":"not_found","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":0,"_primary_term":1}""", 404);

        var result = await provider.RemoveDocumentAsync("1", "people", Token);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveDocumentAsync_IndexNotFound_ReturnsFailure()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [people]"}],"type":"index_not_found_exception","reason":"no such index [people]"},"status":404}""", 404);

        var result = await provider.RemoveDocumentAsync("1", "people", Token);

        result.IsFailed.ShouldBeTrue();
    }
}
