using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchClientSettingsManager(IOptionsMonitor<ElasticSearchOptions> optionsMonitor)
    : IElasticSearchClientSettingsManager
{
    public ElasticSearchIndexSettings GetConnectionSettings(string indexName)
    {
        var options = optionsMonitor.CurrentValue;
        var shared = options.SharedIndexSettings ?? new ElasticSearchIndexSettings();
        if (options.Indexes == null || !options.Indexes.TryGetValue(indexName, out var indexSpecific))
        {
            return MergeSettings(new ElasticSearchIndexSettings(), shared);
        }

        return MergeSettings(indexSpecific, shared);
    }

    public ElasticsearchClientSettings CreateClientSettings(ElasticSearchIndexSettings settings) =>
        ParseMergedSettings(settings);

    public string GetIndexName(string indexName) =>
        IndexNames.WithPrefix(optionsMonitor.CurrentValue.IndexPrefix, indexName);

    /// <summary>
    /// Builds a new settings object, leaving the live options untouched: the index's connection string,
    /// authentication and pool type win, and an index without its own takes the shared one. The additional nodes go with
    /// the connection string: an index with its own connection string uses only its own nodes. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, and the index's write visibility wins over the shared one.
    /// </summary>
    internal static ElasticSearchIndexSettings MergeSettings(ElasticSearchIndexSettings indexSpecific,
        ElasticSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        var ownConnection = !string.IsNullOrWhiteSpace(indexSpecific.ConnectionString);
        return new ElasticSearchIndexSettings
        {
            ConnectionString = ownConnection ? indexSpecific.ConnectionString : shared.ConnectionString,
            AdditionalNodes = ownConnection ? indexSpecific.AdditionalNodes : indexSpecific.AdditionalNodes ?? shared.AdditionalNodes,
            PoolType = indexSpecific.PoolType ?? shared.PoolType,
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? AuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            Mappings = FieldMappings.Merge(shared.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared.WriteVisibility ?? WriteVisibility.Default,
        };
    }

    /// <summary>
    /// The connection setting failures of <paramref name="settings"/>, already merged, each naming the setting to fix.
    /// Checked as OpenSearch does, except that Elastic Cloud connects by its cloud id, so <c>CLOUD</c> and
    /// <c>CLOUDAPI</c> don't need a connection string, and don't use the additional nodes or the pool type. The options validator runs the same checks at startup.
    /// </summary>
    /// <remarks><c>index</c> is the <c>Indexes</c> entry the settings come from, or <c>null</c> when that is not known or they are the shared settings.</remarks>
    internal static IEnumerable<string> ConnectionFailures(ElasticSearchIndexSettings settings, string? index)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var authenticationType = settings.AuthenticationType ?? AuthenticationType.NONE;
        var details = settings.AuthenticationDetails;
        string?[] failures = authenticationType switch
        {
            AuthenticationType.NONE => [.. NodeFailures(settings, index)],
            AuthenticationType.PASSWORD =>
            [
                .. NodeFailures(settings, index),
                ConnectionSettingsChecks.CredentialsFailure(key, index, authenticationType, details,
                    (nameof(AuthenticationDetails.Username), details?.Username), (nameof(AuthenticationDetails.Password), details?.Password)),
            ],
            AuthenticationType.CLOUD =>
            [
                ConnectionSettingsChecks.CredentialsFailure(key, index, authenticationType, details,
                    (nameof(AuthenticationDetails.Id), details?.Id), (nameof(AuthenticationDetails.Username), details?.Username),
                    (nameof(AuthenticationDetails.Password), details?.Password)),
            ],
            AuthenticationType.CLOUDAPI =>
            [
                ConnectionSettingsChecks.CredentialsFailure(key, index, authenticationType, details,
                    (nameof(AuthenticationDetails.Id), details?.Id), (nameof(AuthenticationDetails.ApiKey), details?.ApiKey)),
            ],
            _ => [ConnectionSettingsChecks.UnknownAuthenticationTypeFailure(key, index, settings.AuthenticationType)],
        };
        return failures.OfType<string>();
    }

    private static IEnumerable<string> NodeFailures(ElasticSearchIndexSettings settings, string? index) =>
        ConnectionSettingsChecks.NodeFailures(ElasticSearchOptions.ElasticSearchOptionsKey, index, settings.ConnectionString,
            settings.AdditionalNodes, settings.PoolType);

    /// <summary>
    /// Builds the client settings, throwing an <see cref="ArgumentException"/> that names each setting to fix when
    /// <see cref="ConnectionFailures"/> finds any.
    /// </summary>
    private static ElasticsearchClientSettings ParseMergedSettings(ElasticSearchIndexSettings settings)
    {
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, null));
        var details = settings.AuthenticationDetails!;
        return (settings.AuthenticationType ?? AuthenticationType.NONE) switch
        {
            AuthenticationType.PASSWORD => new ElasticsearchClientSettings(CreateNodePool(settings))
                .Authentication(new BasicAuthentication(details.Username, details.Password)),
            AuthenticationType.CLOUD => new ElasticsearchClientSettings(new CloudNodePool(details.Id, new BasicAuthentication(details.Username, details.Password))),
            AuthenticationType.CLOUDAPI => new ElasticsearchClientSettings(new CloudNodePool(details.Id, new ApiKey(details.ApiKey))),
            _ => new ElasticsearchClientSettings(CreateNodePool(settings)),
        };
    }

    /// <summary>
    /// The pool of the connection string and the additional nodes: a single node pool, as before, when there is only
    /// the connection string and no pool type, and a static pool when there are more nodes and no pool type.
    /// </summary>
    private static NodePool CreateNodePool(ElasticSearchIndexSettings settings)
    {
        var nodes = ConnectionSettingsChecks.NodeUrls(settings.ConnectionString, settings.AdditionalNodes);
        return settings.PoolType switch
        {
            null when nodes.Count == 1 => new SingleNodePool(nodes[0]),
            null or NodePoolType.Static => new StaticNodePool(nodes),
            NodePoolType.Sticky => new StickyNodePool(nodes),
            NodePoolType.Sniffing => new SniffingNodePool(nodes),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.PoolType, "Unknown pool type."),
        };
    }
}
