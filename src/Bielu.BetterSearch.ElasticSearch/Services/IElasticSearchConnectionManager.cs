using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public interface IElasticSearchClientSettingsManager
{
    public ElasticsearchClientSettings GetOrCreateClientSettings(string indexName);
}
