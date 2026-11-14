using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Meilisearch;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Each client owns an <see cref="HttpClient"/>, so clients are cached per server, key and request timeout rather than
/// created per call. The <c>configureClient</c> callbacks given to <c>AddMeilisearch</c> run on each client's
/// <see cref="HttpClient"/> once, when the client is created; two calls racing to create the same client can each run
/// them, and only one client is kept.
/// </summary>
internal sealed class MeilisearchClientFactory(
    IMeilisearchClientSettingsManager settingsManager,
    IEnumerable<ClientConfiguration<HttpClient>> clientConfigurations) : IClientFactoryAsync<MeilisearchClient>
{
    private readonly ConcurrentDictionary<ClientKey, MeilisearchClient> _clients = new();
    private readonly IReadOnlyList<ClientConfiguration<HttpClient>> _clientConfigurations = clientConfigurations.ToList();

    public MeilisearchClientFactory(IMeilisearchClientSettingsManager settingsManager)
        : this(settingsManager, [])
    {
    }

    public Task<MeilisearchClient> GetOrCreateClientAsync(string indexName)
    {
        var settings = settingsManager.GetOrCreateClientSettings(indexName);
        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey;
        return Task.FromResult(_clients.GetOrAdd(new ClientKey(settings.ConnectionString, apiKey, settings.RequestTimeout),
            key => new MeilisearchClient(CreateHttpClient(key), key.ApiKey)));
    }

    /// <summary>
    /// The <see cref="HttpClient"/> a client sends its requests with, built as <see cref="MeilisearchClient(string, string)"/>
    /// builds its own, so Meilisearch errors still throw the client's exceptions.
    /// </summary>
    internal HttpClient CreateHttpClient(ClientKey key)
    {
        var httpClient = new HttpClient(new MeilisearchMessageHandler(new HttpClientHandler())) { BaseAddress = new Uri(key.Url) };
        if (key.RequestTimeout is { } requestTimeout)
        {
            httpClient.Timeout = requestTimeout;
        }

        ClientConfiguration<HttpClient>.Apply(_clientConfigurations, httpClient);
        return httpClient;
    }

    /// <summary>Keeps the key out of <see cref="ToString"/>, so a logged cache key never shows it.</summary>
    internal sealed record ClientKey(string Url, string? ApiKey, TimeSpan? RequestTimeout)
    {
        public override string ToString() => $"{Url} ({RequestTimeout})";
    }
}
