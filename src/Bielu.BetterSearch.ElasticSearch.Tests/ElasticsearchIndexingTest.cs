using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<ElasticsearchClient> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    // Document 0 maps Count as a number, so a text value can't be stored.
    protected override SearchDocument CreateDocumentRejectedByEngine(string index) => new()
    {
        Id = "2",
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { ["Count"] = ["not a number"] },
    };

    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var token = TestContext.Current.CancellationToken;
        (await client.Indices.RefreshAsync(index, token)).IsValidResponse.ShouldBeTrue();
        var count = await client.CountAsync(new CountRequest(index), token);
        count.IsValidResponse.ShouldBeTrue();
        return count.Count;
    }
}
