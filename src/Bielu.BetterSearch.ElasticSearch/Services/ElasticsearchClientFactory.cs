using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticsearchClientFactory : IClientFactory<ElasticsearchClient>
{
    public ElasticsearchClient GetOrCreateClient() => throw new NotImplementedException();
}
