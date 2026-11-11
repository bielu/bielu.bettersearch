using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;
using FluentResults;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Creates one long-lived client per distinct connection, as Elastic recommends. Clients are keyed on the
/// settings an index connects with, not its name: indexes with the same connection string and authentication
/// share a client, and a changed setting, such as a rotated password, gets a new client on the next call.
/// The client for the old setting stays cached until the application restarts, which suits settings that rarely change.
/// </summary>
public class ElasticsearchClientFactory(IElasticSearchClientSettingsManager searchClientSettingsManager) : IClientFactoryAsync<ElasticsearchClient>
{
    private readonly ConcurrentDictionary<ClientKey, Lazy<ElasticsearchClient>> _clients = new();

    public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName)
    {
        var settings = searchClientSettingsManager.GetConnectionSettings(indexName);
        // PublicationOnly does not cache an exception, so invalid settings fail each call until they are fixed.
        var client = _clients.GetOrAdd(ClientKey.From(settings), _ => new Lazy<ElasticsearchClient>(
            () => new ElasticsearchClient(searchClientSettingsManager.CreateClientSettings(settings)),
            LazyThreadSafetyMode.PublicationOnly));
        return Task.FromResult(client.Value);
    }

    internal readonly record struct ClientKey(
        string ConnectionString,
        AuthenticationType? AuthenticationType,
        string? Id,
        string? Username,
        string? Password,
        string? ApiKey)
    {
        public static ClientKey From(ElasticSearchIndexSettings settings) => new(
            settings.ConnectionString,
            settings.AuthenticationType,
            settings.AuthenticationDetails?.Id,
            settings.AuthenticationDetails?.Username,
            settings.AuthenticationDetails?.Password,
            settings.AuthenticationDetails?.ApiKey);

        // The generated ToString prints every member; keep the credentials out of logs and exception messages.
        public override string ToString() =>
            $"{nameof(ClientKey)} {{ {nameof(ConnectionString)} = {ConnectionString}, {nameof(AuthenticationType)} = {AuthenticationType} }}";
    }
}

internal static class ElasticsearchClientFactoryExtensions
{
    /// <summary>Gets the index's client, or a failed <see cref="Result"/> that keeps the exception when it cannot be created.</summary>
    /// <remarks>
    /// Throws when <paramref name="cancellationToken"/> is cancelled: the Elasticsearch transport reports a cancelled
    /// request as a failed response rather than throwing, so cancellation is checked before the request is made.
    /// </remarks>
    public static async Task<Result<ElasticsearchClient>> TryGetClientAsync(this IClientFactoryAsync<ElasticsearchClient> clientFactory, string indexName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Result.Ok(await clientFactory.GetOrCreateClientAsync(indexName).ConfigureAwait(false));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<ElasticsearchClient>(new Error(e.Message).CausedBy(e));
        }
    }
}
