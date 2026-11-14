using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;
using FluentResults;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Creates one long-lived client per distinct connection, as Elastic recommends. Clients are keyed on the
/// settings an index connects with, not its name: indexes with the same nodes, pool type and authentication
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
        string AdditionalNodes,
        NodePoolType? PoolType,
        AuthenticationType? AuthenticationType,
        string? Id,
        string? Username,
        string? Password,
        string? ApiKey)
    {
        public static ClientKey From(ElasticSearchIndexSettings settings) => new(
            settings.ConnectionString,
            // A record compares a list by reference, so the nodes are compared as one string. Each node is prefixed with its
            // length, because a URL that passes validation can hold any separator, a space included.
            string.Concat((settings.AdditionalNodes ?? []).Select(node => $"{node?.Length}:{node};")),
            settings.PoolType,
            settings.AuthenticationType,
            settings.AuthenticationDetails?.Id,
            settings.AuthenticationDetails?.Username,
            settings.AuthenticationDetails?.Password,
            settings.AuthenticationDetails?.ApiKey);

        // The generated ToString prints every member; keep the credentials out of logs and exception messages.
        public override string ToString() =>
            $"{nameof(ClientKey)} {{ {nameof(ConnectionString)} = {ConnectionString}, {nameof(AdditionalNodes)} = {AdditionalNodes}, {nameof(PoolType)} = {PoolType}, {nameof(AuthenticationType)} = {AuthenticationType} }}";
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

    /// <summary>
    /// Runs <paramref name="request"/> with the index's client, returning a failed <see cref="Result"/> that keeps the
    /// exception when the client cannot be created or the request throws, as it does with <c>ThrowExceptions()</c> on.
    /// </summary>
    /// <remarks>
    /// An <see cref="OperationCanceledException"/> is rethrown rather than turned into a failure, and one is thrown when
    /// <paramref name="cancellationToken"/> is cancelled by the time the request returns, because the client reports a
    /// request cancelled in flight as a failed response.
    /// </remarks>
    public static async Task<Result<T>> TryWithClientAsync<T>(this IClientFactoryAsync<ElasticsearchClient> clientFactory, string indexName,
        Func<ElasticsearchClient, Task<Result<T>>> request, CancellationToken cancellationToken)
    {
        var client = await clientFactory.TryGetClientAsync(indexName, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<T>();
        }

        try
        {
            var result = await request(client.Value).ConfigureAwait(false);
            // The client reports a request cancelled in flight as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<T>(new Error(e.Message).CausedBy(e));
        }
    }

    /// <inheritdoc cref="TryWithClientAsync{T}"/>
    public static async Task<Result> TryWithClientAsync(this IClientFactoryAsync<ElasticsearchClient> clientFactory, string indexName,
        Func<ElasticsearchClient, Task<Result>> request, CancellationToken cancellationToken)
    {
        var client = await clientFactory.TryGetClientAsync(indexName, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult();
        }

        try
        {
            var result = await request(client.Value).ConfigureAwait(false);
            // The client reports a request cancelled in flight as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }
    }
}
