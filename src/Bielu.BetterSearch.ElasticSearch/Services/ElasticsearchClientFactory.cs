using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;
using FluentResults;
using Microsoft.Extensions.Options;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Creates one long-lived client per connection, as Elastic recommends: a shared client for every index
/// without a connection string of its own, and one for each index that has one.
/// </summary>
public class ElasticsearchClientFactory(IElasticSearchClientSettingsManager searchClientSettingsManager, IOptionsMonitor<ElasticSearchOptions> optionsMonitor) : IClientFactoryAsync<ElasticsearchClient>
{
    private const string DefaultKey = "default";
    private readonly ConcurrentDictionary<string, Lazy<ElasticsearchClient>> _clients = new();

    public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName)
    {
        var key = optionsMonitor.CurrentValue.Indexes == null || !optionsMonitor.CurrentValue.Indexes.TryGetValue(indexName, out var indexSettings) ||
            string.IsNullOrWhiteSpace(indexSettings.ConnectionString)
            ? DefaultKey
            : indexName;

        var client = _clients.GetOrAdd(key, settingsKey => new Lazy<ElasticsearchClient>(() =>
            new ElasticsearchClient(searchClientSettingsManager.GetOrCreateClientSettings(settingsKey))));
        return Task.FromResult(client.Value);
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
