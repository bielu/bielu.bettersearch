using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Typesense.Configuration;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Each client owns an <see cref="HttpClient"/>, so clients are cached per set of nodes, key and timeout rather than
/// created per call. The <c>configureClient</c> callbacks given to <c>AddTypesense</c> run on each client's
/// <see cref="HttpClient"/> once, when the client is created. When one throws, the client is not cached, so the next call
/// creates it again and runs them again.
/// </summary>
internal sealed class TypesenseClientFactory(
    ITypesenseClientSettingsManager settingsManager,
    IOptionsMonitor<TypesenseOptions> optionsMonitor,
    IEnumerable<ClientConfiguration<HttpClient>> clientConfigurations) : IClientFactoryAsync<TypesenseHttpClientWrapper>, IDisposable
{
    private readonly ConcurrentDictionary<ClientKey, Lazy<TypesenseHttpClientWrapper>> _clients = new();
    private readonly ConcurrentBag<HttpClient> _httpClients = [];
    private readonly IReadOnlyList<ClientConfiguration<HttpClient>> _clientConfigurations = clientConfigurations.ToList();

    public TypesenseClientFactory(ITypesenseClientSettingsManager settingsManager, IOptionsMonitor<TypesenseOptions> optionsMonitor)
        : this(settingsManager, optionsMonitor, [])
    {
    }

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
        var client = _clients.GetOrAdd(key, k => new Lazy<TypesenseHttpClientWrapper>(() => CreateClient(k, nodes)));
        try
        {
            return Task.FromResult(client.Value);
        }
        catch
        {
            // A Lazy keeps the exception it threw, so drop it, or every later call would get the same exception.
            _clients.TryRemove(new KeyValuePair<ClientKey, Lazy<TypesenseHttpClientWrapper>>(key, client));
            throw;
        }
    }

    private TypesenseHttpClientWrapper CreateClient(ClientKey key, List<Uri> nodes)
    {
        var httpClient = new HttpClient { Timeout = key.Timeout > TimeSpan.Zero ? key.Timeout : Timeout.InfiniteTimeSpan };
        try
        {
            ClientConfiguration<HttpClient>.Apply(_clientConfigurations, httpClient);
        }
        catch
        {
            httpClient.Dispose();
            throw;
        }

        _httpClients.Add(httpClient);
        return new TypesenseHttpClientWrapper(httpClient, nodes, key.ApiKey);
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
