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
/// The client for the old setting stays cached until the application restarts, which suits settings that rarely change.
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

    internal readonly record struct ClientKey(
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

        // The generated ToString prints every member; keep the credentials out of logs and exception messages.
        public override string ToString() =>
            $"{nameof(ClientKey)} {{ {nameof(ConnectionString)} = {ConnectionString}, {nameof(AuthenticationType)} = {AuthenticationType} }}";
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
    /// <remarks>An <see cref="OperationCanceledException"/> is rethrown rather than turned into a failure.</remarks>
    public static async Task<Result<T>> TryWithClientAsync<T>(this IClientFactoryAsync<IOpenSearchClient> clientFactory, string indexName,
        Func<IOpenSearchClient, Task<Result<T>>> request)
    {
        try
        {
            var client = await clientFactory.TryGetClientAsync(indexName).ConfigureAwait(false);
            return client.IsFailed ? client.ToResult<T>() : await request(client.Value).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<T>(new Error(e.Message).CausedBy(e));
        }
    }

    /// <inheritdoc cref="TryWithClientAsync{T}"/>
    public static async Task<Result> TryWithClientAsync(this IClientFactoryAsync<IOpenSearchClient> clientFactory, string indexName,
        Func<IOpenSearchClient, Task<Result>> request)
    {
        try
        {
            var client = await clientFactory.TryGetClientAsync(indexName).ConfigureAwait(false);
            return client.IsFailed ? client.ToResult() : await request(client.Value).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }
    }
}
