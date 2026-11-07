using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<ElasticsearchClient> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    protected override async Task DocumentsChangedAsync(string index) =>
        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index);
}
