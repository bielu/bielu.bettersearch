using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticsearchClientFactory(IElasticSearchClientSettingsManager searchClientSettingsManager, IOptionsMonitor<ElasticSearchOptions> optionsMonitor) : IClientFactoryAsync<ElasticsearchClient>
{
    public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName)
    {
        var defaultSettings = searchClientSettingsManager.GetOrCreateClientSettings("default");
        if (!optionsMonitor.CurrentValue.Indexes.TryGetValue(indexName, out var indexSettings) ||
            !indexSettings.CustomConnectionString)
        {
            return Task.FromResult(new ElasticsearchClient(defaultSettings));
        }

        return Task.FromResult(new ElasticsearchClient(searchClientSettingsManager.GetOrCreateClientSettings(indexName)));
    }
}
