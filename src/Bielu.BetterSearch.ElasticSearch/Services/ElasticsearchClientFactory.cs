using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticsearchClientFactory : IClientFactoryAsync<ElasticsearchClient>
{
    public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName) => throw new NotImplementedException();
}
