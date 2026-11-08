using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public interface IElasticSearchClientSettingsManager
{
    public ElasticsearchClientSettings GetOrCreateClientSettings(string indexName);

    /// <summary>The settings for the client shared by every index without a connection string of its own.</summary>
    public ElasticsearchClientSettings GetOrCreateSharedClientSettings();
}
