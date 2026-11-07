using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Creates one long-lived client per settings, so connections are reused: a shared client for every index
/// without settings of its own, and one for each index that has them.
/// </summary>
internal sealed class OpenSearchClientFactory(IOpenSearchClientSettingsManager settingsManager, IOptionsMonitor<OpenSearchOptions> optionsMonitor) : IClientFactoryAsync<IOpenSearchClient>
{
    private const string DefaultKey = "default";
    private readonly ConcurrentDictionary<string, Lazy<IOpenSearchClient>> _clients = new();

    public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName)
    {
        var key = optionsMonitor.CurrentValue.Indexes?.ContainsKey(indexName) == true ? indexName : DefaultKey;
        var client = _clients.GetOrAdd(key, settingsKey => new Lazy<IOpenSearchClient>(() =>
            new OpenSearchClient(settingsManager.GetOrCreateClientSettings(settingsKey))));
        return Task.FromResult(client.Value);
    }
}
