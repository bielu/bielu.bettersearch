using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Creates one long-lived client per distinct connection, so connections are reused. Clients are keyed on the
/// settings an index connects with, not its name: indexes with the same nodes, pool type and authentication
/// share a client, and a changed setting, such as a rotated password, gets a new client on the next call.
/// The client for the old setting stays cached until the application restarts, which suits settings that rarely change.
/// The request timeout and retry count are part of the key, so indexes that set different ones get different clients.
/// The <c>configureClient</c> callbacks given to <c>AddOpenSearch</c> run on each client's settings once, when the
/// client is created; two calls racing to create the same client can each run them, and only one client is kept.
/// </summary>
internal sealed class OpenSearchClientFactory(
    IOpenSearchClientSettingsManager settingsManager,
    IEnumerable<ClientConfiguration<ConnectionSettings>> clientConfigurations) : IClientFactoryAsync<IOpenSearchClient>
{
    private readonly IReadOnlyList<ClientConfiguration<ConnectionSettings>> _clientConfigurations = clientConfigurations.ToList();

    public OpenSearchClientFactory(IOpenSearchClientSettingsManager settingsManager)
        : this(settingsManager, [])
    {
    }

    private readonly ConcurrentDictionary<ClientKey, Lazy<IOpenSearchClient>> _clients = new();

    public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName)
    {
        var settings = settingsManager.GetConnectionSettings(indexName);
        // PublicationOnly does not cache an exception, so invalid settings fail each call until they are fixed.
        var client = _clients.GetOrAdd(ClientKey.From(settings), _ => new Lazy<IOpenSearchClient>(
            () =>
            {
                var connectionSettings = settingsManager.CreateClientSettings(settings);
                ClientConfiguration<ConnectionSettings>.Apply(_clientConfigurations, connectionSettings);
                return new OpenSearchClient(connectionSettings);
            },
            LazyThreadSafetyMode.PublicationOnly));
        return Task.FromResult(client.Value);
    }

    internal readonly record struct ClientKey(
        string ConnectionString,
        string AdditionalNodes,
        NodePoolType? PoolType,
        OpenSearchAuthenticationType? AuthenticationType,
        string? Username,
        string? Password,
        string? ApiKey,
        TimeSpan? RequestTimeout,
        int? MaxRetries)
    {
        public static ClientKey From(OpenSearchIndexSettings settings) => new(
            settings.ConnectionString,
            // A record compares a list by reference, so the nodes are compared as one string. Each node is prefixed with its
            // length, because a URL that passes validation can hold any separator, a space included.
            string.Concat((settings.AdditionalNodes ?? []).Select(node => $"{node?.Length}:{node};")),
            settings.PoolType,
            settings.AuthenticationType,
            settings.AuthenticationDetails?.Username,
            settings.AuthenticationDetails?.Password,
            settings.AuthenticationDetails?.ApiKey,
            settings.RequestTimeout,
            settings.MaxRetries);

        // The generated ToString prints every member; keep the credentials out of logs and exception messages.
        public override string ToString() =>
            $"{nameof(ClientKey)} {{ {nameof(ConnectionString)} = {ConnectionString}, {nameof(AdditionalNodes)} = {AdditionalNodes}, {nameof(PoolType)} = {PoolType}, {nameof(AuthenticationType)} = {AuthenticationType}, {nameof(RequestTimeout)} = {RequestTimeout}, {nameof(MaxRetries)} = {MaxRetries} }}";
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

    /// <summary>
    /// Runs <paramref name="request"/> with the index's client, returning a failed <see cref="Result"/> that keeps the
    /// exception when the client cannot be created or the request throws, as it does with <c>ThrowExceptions()</c> on.
    /// </summary>
    /// <remarks>
    /// An <see cref="OperationCanceledException"/> is rethrown rather than turned into a failure, and one is thrown when
    /// <paramref name="cancellationToken"/> is cancelled by the time the request returns, because the client reports a
    /// request cancelled in flight as a failed response.
    /// </remarks>
    public static async Task<Result<T>> TryWithClientAsync<T>(this IClientFactoryAsync<IOpenSearchClient> clientFactory, string indexName,
        Func<IOpenSearchClient, Task<Result<T>>> request, CancellationToken cancellationToken)
    {
        try
        {
            var client = await clientFactory.TryGetClientAsync(indexName).ConfigureAwait(false);
            if (client.IsFailed)
            {
                return client.ToResult<T>();
            }

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
    public static async Task<Result> TryWithClientAsync(this IClientFactoryAsync<IOpenSearchClient> clientFactory, string indexName,
        Func<IOpenSearchClient, Task<Result>> request, CancellationToken cancellationToken)
    {
        try
        {
            var client = await clientFactory.TryGetClientAsync(indexName).ConfigureAwait(false);
            if (client.IsFailed)
            {
                return client.ToResult();
            }

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
