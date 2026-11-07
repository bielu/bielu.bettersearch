using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Creates one long-lived client per distinct connection, so connections are reused. Clients are keyed on the
/// settings an index connects with, not its name: indexes with the same connection string and authentication
/// share a client, and a changed setting, such as a rotated password, gets a new client on the next call.
/// </summary>
internal sealed class OpenSearchClientFactory(IOpenSearchClientSettingsManager settingsManager) : IClientFactoryAsync<IOpenSearchClient>
{
    private readonly ConcurrentDictionary<ClientKey, Lazy<IOpenSearchClient>> _clients = new();

    public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName)
    {
        var settings = settingsManager.GetConnectionSettings(indexName);
        // PublicationOnly does not cache an exception, so invalid settings fail each call until they are fixed.
        var client = _clients.GetOrAdd(ClientKey.From(settings), _ => new Lazy<IOpenSearchClient>(
            () => new OpenSearchClient(settingsManager.CreateClientSettings(settings)),
            LazyThreadSafetyMode.PublicationOnly));
        return Task.FromResult(client.Value);
    }

    private readonly record struct ClientKey(
        string ConnectionString,
        OpenSearchAuthenticationType? AuthenticationType,
        string? Username,
        string? Password,
        string? ApiKey)
    {
        public static ClientKey From(OpenSearchIndexSettings settings) => new(
            settings.ConnectionString,
            settings.AuthenticationType,
            settings.AuthenticationDetails?.Username,
            settings.AuthenticationDetails?.Password,
            settings.AuthenticationDetails?.ApiKey);
    }
}

internal static class OpenSearchClientFactoryExtensions
{
    /// <summary>Gets the index's client, or a failed <see cref="Result"/> when its settings are invalid.</summary>
    public static async Task<Result<IOpenSearchClient>> TryGetClientAsync(this IClientFactoryAsync<IOpenSearchClient> clientFactory, string indexName)
    {
        try
        {
            return Result.Ok(await clientFactory.GetOrCreateClientAsync(indexName).ConfigureAwait(false));
        }
        catch (ArgumentException e)
        {
            return Result.Fail<IOpenSearchClient>(new Error($"The OpenSearch settings for {indexName} are invalid: {e.Message}"));
        }
    }
}
