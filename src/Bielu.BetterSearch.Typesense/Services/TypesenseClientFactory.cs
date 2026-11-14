using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Typesense.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Each client owns an <see cref="HttpClient"/>, so clients are cached per set of nodes, key and timeout rather than
/// created per call.
/// </summary>
internal sealed class TypesenseClientFactory(
    ITypesenseClientSettingsManager settingsManager,
    IOptionsMonitor<TypesenseOptions> optionsMonitor) : IClientFactoryAsync<TypesenseHttpClientWrapper>, IDisposable
{
    private readonly ConcurrentDictionary<ClientKey, Lazy<TypesenseHttpClientWrapper>> _clients = new();
    private readonly ConcurrentBag<HttpClient> _httpClients = [];

    public Task<TypesenseHttpClientWrapper> GetOrCreateClientAsync(string indexName)
    {
        var settings = settingsManager.GetOrCreateClientSettings(indexName);
        var nodes = new List<Uri> { ConnectionSettingsChecks.HttpUrl(TypesenseOptions.TypesenseOptionsKey, settings.ConnectionString) };
        foreach (var node in settings.AdditionalNodes ?? [])
        {
            nodes.Add(ConnectionSettingsChecks.HttpUrl(TypesenseOptions.TypesenseOptionsKey, node));
        }

        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey;
        var key = new ClientKey(string.Join(' ', nodes), apiKey, optionsMonitor.CurrentValue.RequestTimeout);
        return Task.FromResult(_clients.GetOrAdd(key, k => new Lazy<TypesenseHttpClientWrapper>(() =>
        {
            var httpClient = new HttpClient { Timeout = k.Timeout > TimeSpan.Zero ? k.Timeout : Timeout.InfiniteTimeSpan };
            _httpClients.Add(httpClient);
            return new TypesenseHttpClientWrapper(httpClient, nodes, k.ApiKey);
        })).Value);
    }

    public void Dispose()
    {
        foreach (var httpClient in _httpClients)
        {
            httpClient.Dispose();
        }
    }

    /// <summary>Keeps the key out of <see cref="ToString"/>, so a logged cache key never shows it.</summary>
    private sealed record ClientKey(string Nodes, string? ApiKey, TimeSpan Timeout)
    {
        public override string ToString() => $"{Nodes} ({Timeout})";
    }
}
