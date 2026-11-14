using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public interface IElasticSearchClientSettingsManager
{
    /// <summary>
    /// The settings an index connects with: its own, with what it leaves out taken from the shared ones.
    /// Returns a new object, so the monitored options are never changed.
    /// </summary>
    public ElasticSearchIndexSettings GetConnectionSettings(string indexName);

    /// <summary>Builds client settings, throwing when the settings are invalid.</summary>
    public ElasticsearchClientSettings CreateClientSettings(ElasticSearchIndexSettings settings);

    /// <summary>
    /// The name Elasticsearch knows <paramref name="indexName"/> by, which every request uses: the configured
    /// <see cref="ElasticSearchOptions.IndexPrefix"/> followed by the name. The default implementation adds no prefix.
    /// </summary>
    public string GetIndexName(string indexName) => indexName;
}
