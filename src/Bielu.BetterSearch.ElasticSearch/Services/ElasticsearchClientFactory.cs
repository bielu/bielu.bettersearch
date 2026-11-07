using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Creates one long-lived client per connection, as Elastic recommends: a shared client for every index
/// without a connection string of its own, and one for each index that has one.
/// </summary>
public class ElasticsearchClientFactory(IElasticSearchClientSettingsManager searchClientSettingsManager, IOptionsMonitor<ElasticSearchOptions> optionsMonitor) : IClientFactoryAsync<ElasticsearchClient>
{
    private const string DefaultKey = "default";
    private readonly ConcurrentDictionary<string, Lazy<ElasticsearchClient>> _clients = new();

    public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName)
    {
        var key = optionsMonitor.CurrentValue.Indexes == null || !optionsMonitor.CurrentValue.Indexes.TryGetValue(indexName, out var indexSettings) ||
            string.IsNullOrWhiteSpace(indexSettings.ConnectionString)
            ? DefaultKey
            : indexName;

        var client = _clients.GetOrAdd(key, settingsKey => new Lazy<ElasticsearchClient>(() =>
            new ElasticsearchClient(searchClientSettingsManager.GetOrCreateClientSettings(settingsKey))));
        return Task.FromResult(client.Value);
    }
}
