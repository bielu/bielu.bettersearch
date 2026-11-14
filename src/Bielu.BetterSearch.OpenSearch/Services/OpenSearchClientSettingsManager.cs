using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchClientSettingsManager(IOptionsMonitor<OpenSearchOptions> optionsMonitor)
    : IOpenSearchClientSettingsManager
{
    public OpenSearchIndexSettings GetConnectionSettings(string indexName)
    {
        var options = optionsMonitor.CurrentValue;
        var shared = options.SharedIndexSettings;
        if (options.Indexes == null || !options.Indexes.TryGetValue(indexName, out var indexSpecific))
        {
            return MergeSettings(new OpenSearchIndexSettings(), shared);
        }

        return MergeSettings(indexSpecific, shared);
    }

    public string GetIndexName(string indexName) =>
        IndexNames.WithPrefix(optionsMonitor.CurrentValue.IndexPrefix, indexName);

    /// <summary>
    /// Builds a new settings object, leaving the live options untouched: the index's connection string,
    /// authentication and pool type win, and an index without its own takes the shared one. The additional nodes go with
    /// the connection string: an index with its own connection string uses only its own nodes. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, the index's write visibility wins over the shared one, and so do its request timeout and retry count.
    /// </summary>
    internal static OpenSearchIndexSettings MergeSettings(OpenSearchIndexSettings indexSpecific,
        OpenSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        var ownConnection = !string.IsNullOrWhiteSpace(indexSpecific.ConnectionString);
        return new OpenSearchIndexSettings
        {
            ConnectionString = ownConnection ? indexSpecific.ConnectionString : shared.ConnectionString,
            // A copy, so a change to the returned list leaves the options, and every other index's nodes, untouched.
            AdditionalNodes = (ownConnection ? indexSpecific.AdditionalNodes : indexSpecific.AdditionalNodes ?? shared.AdditionalNodes)?.ToList(),
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? OpenSearchAuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            PoolType = indexSpecific.PoolType ?? shared?.PoolType,
            Mappings = FieldMappings.Merge(shared?.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared?.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared?.WriteVisibility ?? WriteVisibility.Default,
            RequestTimeout = indexSpecific.RequestTimeout ?? shared?.RequestTimeout,
            MaxRetries = indexSpecific.MaxRetries ?? shared?.MaxRetries,
        };
    }

    /// <summary>
    /// The connection setting failures of <paramref name="settings"/>, already merged, each naming the setting to fix.
    /// The options validator runs the same checks at startup.
    /// </summary>
    /// <remarks>
    /// <c>connection</c> is where the connection string comes from, and <c>authentication</c> where the authentication
    /// type and details come from, which an <c>Indexes</c> entry takes together. <c>nodes</c> and <c>pool</c> are where the
    /// additional nodes and the pool type come from, the connection string's source when not given.
    /// </remarks>
    internal static IEnumerable<string> ConnectionFailures(OpenSearchIndexSettings settings, SettingSource connection, SettingSource authentication,
        SettingSource? nodes = null, SettingSource? pool = null)
    {
        var key = OpenSearchOptions.OpenSearchOptionsKey;
        var authenticationType = settings.AuthenticationType ?? OpenSearchAuthenticationType.NONE;
        var details = settings.AuthenticationDetails;
        string?[] failures =
        [
            .. ConnectionSettingsChecks.NodeFailures(key, connection, nodes ?? connection, pool ?? connection, settings.ConnectionString, settings.AdditionalNodes, settings.PoolType),
            authenticationType switch
            {
                OpenSearchAuthenticationType.NONE => null,
                OpenSearchAuthenticationType.PASSWORD => ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(OpenSearchAuthenticationDetails.Username), details?.Username),
                    (nameof(OpenSearchAuthenticationDetails.Password), details?.Password)),
                OpenSearchAuthenticationType.APIKEY => ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(OpenSearchAuthenticationDetails.ApiKey), details?.ApiKey)),
                _ => ConnectionSettingsChecks.UnknownAuthenticationTypeFailure(key, authentication, settings.AuthenticationType),
            },
        ];
        return failures.OfType<string>();
    }

    public ConnectionSettings CreateClientSettings(OpenSearchIndexSettings settings)
    {
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, SettingSource.Unknown, SettingSource.Unknown));
        var connectionSettings = new ConnectionSettings(CreateConnectionPool(settings));
        var details = settings.AuthenticationDetails!;
        switch (settings.AuthenticationType ?? OpenSearchAuthenticationType.NONE)
        {
            case OpenSearchAuthenticationType.PASSWORD:
                connectionSettings.BasicAuthentication(details.Username, details.Password);
                break;
            case OpenSearchAuthenticationType.APIKEY:
                // OpenSearch API keys are sent as they are, "Authorization: ApiKey <token>", not encoded with an id.
                connectionSettings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(details.ApiKey));
                break;
        }

        if (settings.RequestTimeout is { } requestTimeout)
        {
            connectionSettings.RequestTimeout(requestTimeout);
        }

        if (settings.MaxRetries is { } maxRetries)
        {
            connectionSettings.MaximumRetries(maxRetries);
        }

        return connectionSettings;
    }

    /// <summary>
    /// The pool of the connection string and the additional nodes: a single node pool, as before, when there is only
    /// the connection string and no pool type, and a static pool when there are more nodes and no pool type.
    /// </summary>
    private static IConnectionPool CreateConnectionPool(OpenSearchIndexSettings settings)
    {
        var nodes = ConnectionSettingsChecks.NodeUrls(settings.ConnectionString, settings.AdditionalNodes);
        return settings.PoolType switch
        {
            null when nodes.Count == 1 => new SingleNodeConnectionPool(nodes[0]),
            null or NodePoolType.Static => new StaticConnectionPool(nodes),
            NodePoolType.Sticky => new StickyConnectionPool(nodes),
            NodePoolType.Sniffing => new SniffingConnectionPool(nodes),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.PoolType, "Unknown pool type."),
        };
    }
}
