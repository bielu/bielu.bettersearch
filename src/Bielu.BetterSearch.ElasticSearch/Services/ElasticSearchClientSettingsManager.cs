using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchClientSettingsManager : IElasticSearchClientSettingsManager
{
    public ElasticsearchClientSettings GetOrCreateClientSettings(string indexName) => throw new NotImplementedException();
}
