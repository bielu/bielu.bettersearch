using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchQueryResultTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<ElasticsearchClient> clientFactory)
    : QueryResultBaseTests(serviceAsync, indexingServiceAsync, indexingProviderAsync)
{
    protected override async Task DocumentsIndexedAsync(string index) =>
        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index);
}
