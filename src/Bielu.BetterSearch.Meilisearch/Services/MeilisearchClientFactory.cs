using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Meilisearch;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Each client owns an <see cref="HttpClient"/>, so clients are cached per server and key rather than
/// created per call.
/// </summary>
internal sealed class MeilisearchClientFactory(IMeilisearchClientSettingsManager settingsManager) : IClientFactoryAsync<MeilisearchClient>
{
    private readonly ConcurrentDictionary<(string Url, string? ApiKey), MeilisearchClient> _clients = new();

    public Task<MeilisearchClient> GetOrCreateClientAsync(string indexName)
    {
        var settings = settingsManager.GetOrCreateClientSettings(indexName);
        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey;
        return Task.FromResult(_clients.GetOrAdd((settings.ConnectionString, apiKey),
            key => new MeilisearchClient(key.Url, key.ApiKey)));
    }
}
