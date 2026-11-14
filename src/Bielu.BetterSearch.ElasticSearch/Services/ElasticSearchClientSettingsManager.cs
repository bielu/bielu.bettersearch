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
    /// the connection string: an index with its own connection string uses only its own nodes. The TLS settings come from where the endpoint does: the index's own when it sets a connection string or cloud authentication, else the shared ones. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, the index's write visibility wins over the shared one, and so do its request timeout and retry count.
    /// </summary>
    internal static ElasticSearchIndexSettings MergeSettings(ElasticSearchIndexSettings indexSpecific,
        ElasticSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        var ownConnection = !string.IsNullOrWhiteSpace(indexSpecific.ConnectionString);
        // The TLS settings belong to the server, so they come with the endpoint: the connection string, or the cloud id.
        var tls = HasOwnEndpoint(indexSpecific) ? indexSpecific : shared;
        return new ElasticSearchIndexSettings
        {
            ConnectionString = ownConnection ? indexSpecific.ConnectionString : shared.ConnectionString,
            // A copy, so a change to the returned list leaves the options, and every other index's nodes, untouched.
            AdditionalNodes = (ownConnection ? indexSpecific.AdditionalNodes : indexSpecific.AdditionalNodes ?? shared.AdditionalNodes)?.ToList(),
            PoolType = indexSpecific.PoolType ?? shared.PoolType,
            CertificateFingerprint = tls.CertificateFingerprint,
            DevelopmentOnlySkipCertificateValidation = tls.DevelopmentOnlySkipCertificateValidation ?? false,
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? AuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            Mappings = FieldMappings.Merge(shared.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared.WriteVisibility ?? WriteVisibility.Default,
            RequestTimeout = indexSpecific.RequestTimeout ?? shared.RequestTimeout,
            MaxRetries = indexSpecific.MaxRetries ?? shared.MaxRetries,
        };
    }

    /// <summary>
    /// Whether <paramref name="settings"/> name a server of their own: a connection string, or <c>CLOUD</c> or
    /// <c>CLOUDAPI</c> authentication, which connects by its cloud id. Only then do they use their own TLS settings.
    /// </summary>
    internal static bool HasOwnEndpoint(ElasticSearchIndexSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ConnectionString) ||
        settings.AuthenticationType is AuthenticationType.CLOUD or AuthenticationType.CLOUDAPI;

    /// <summary>
    /// The connection setting failures of <paramref name="settings"/>, already merged, each naming the setting to fix.
    /// Checked as OpenSearch does, except that Elastic Cloud connects by its cloud id, so <c>CLOUD</c> and
    /// <c>CLOUDAPI</c> don't need a connection string, and don't use the additional nodes or the pool type. The options validator runs the same checks at startup.
    /// </summary>
    /// <remarks>
    /// <c>connection</c> is where the connection string comes from, and <c>authentication</c> where the authentication
    /// type and details come from, which an <c>Indexes</c> entry takes together. <c>nodes</c> and <c>pool</c> are where the
    /// additional nodes and the pool type come from, the connection string's source when not given.
    /// </remarks>
    internal static IEnumerable<string> ConnectionFailures(ElasticSearchIndexSettings settings, SettingSource connection, SettingSource authentication,
        SettingSource? nodes = null, SettingSource? pool = null)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var authenticationType = settings.AuthenticationType ?? AuthenticationType.NONE;
        var details = settings.AuthenticationDetails;
        string?[] failures = authenticationType switch
        {
            AuthenticationType.NONE => [.. NodeFailures(settings, connection, nodes ?? connection, pool ?? connection)],
            AuthenticationType.PASSWORD =>
            [
                .. NodeFailures(settings, connection, nodes ?? connection, pool ?? connection),
                ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(AuthenticationDetails.Username), details?.Username), (nameof(AuthenticationDetails.Password), details?.Password)),
            ],
            AuthenticationType.CLOUD =>
            [
                ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(AuthenticationDetails.Id), details?.Id), (nameof(AuthenticationDetails.Username), details?.Username),
                    (nameof(AuthenticationDetails.Password), details?.Password)),
            ],
            AuthenticationType.CLOUDAPI =>
            [
                ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(AuthenticationDetails.Id), details?.Id), (nameof(AuthenticationDetails.ApiKey), details?.ApiKey)),
            ],
            AuthenticationType.APIKEY =>
            [
                .. NodeFailures(settings, connection, nodes ?? connection, pool ?? connection),
                ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(AuthenticationDetails.ApiKey), details?.ApiKey)),
            ],
            AuthenticationType.BEARER =>
            [
                .. NodeFailures(settings, connection, nodes ?? connection, pool ?? connection),
                ConnectionSettingsChecks.CredentialsFailure(key, authentication, authenticationType, details,
                    (nameof(AuthenticationDetails.Token), details?.Token)),
            ],
            _ => [ConnectionSettingsChecks.UnknownAuthenticationTypeFailure(key, authentication, settings.AuthenticationType)],
        };
        // The TLS settings go with the endpoint, so an entry sets them itself when it has its own connection string or cloud id.
        var tls = connection with
        {
            Inherited = connection.Inherited && (authentication.Inherited || authenticationType is not (AuthenticationType.CLOUD or AuthenticationType.CLOUDAPI)),
        };
        return failures.OfType<string>()
            .Concat(ConnectionSettingsChecks.TlsFailures(key, tls, settings.CertificateFingerprint, settings.DevelopmentOnlySkipCertificateValidation));
    }

    private static IEnumerable<string> NodeFailures(ElasticSearchIndexSettings settings, SettingSource connection, SettingSource nodes, SettingSource pool) =>
        ConnectionSettingsChecks.NodeFailures(ElasticSearchOptions.ElasticSearchOptionsKey, connection, nodes, pool, settings.ConnectionString,
            settings.AdditionalNodes, settings.PoolType);

    /// <summary>
    /// Builds the client settings, with the request timeout and retry count when they are set, throwing an
    /// <see cref="ArgumentException"/> that names each setting to fix when <see cref="ConnectionFailures"/> finds any.
    /// </summary>
    private static ElasticsearchClientSettings ParseMergedSettings(ElasticSearchIndexSettings settings)
    {
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, SettingSource.Unknown, SettingSource.Unknown));
        var clientSettings = ConnectedSettings(settings);
        if (settings.RequestTimeout is { } requestTimeout)
        {
            clientSettings.RequestTimeout(requestTimeout);
        }

        if (settings.MaxRetries is { } maxRetries)
        {
            clientSettings.MaximumRetries(maxRetries);
        }

        return clientSettings;
    }

    private static ElasticsearchClientSettings ConnectedSettings(ElasticSearchIndexSettings settings)
    {
        var details = settings.AuthenticationDetails!;
        var clientSettings = (settings.AuthenticationType ?? AuthenticationType.NONE) switch
        {
            AuthenticationType.PASSWORD => new ElasticsearchClientSettings(CreateNodePool(settings))
                .Authentication(new BasicAuthentication(details.Username, details.Password)),
            AuthenticationType.CLOUD => new ElasticsearchClientSettings(new CloudNodePool(details.Id, new BasicAuthentication(details.Username, details.Password))),
            AuthenticationType.CLOUDAPI => new ElasticsearchClientSettings(new CloudNodePool(details.Id, new ApiKey(details.ApiKey))),
            AuthenticationType.APIKEY => new ElasticsearchClientSettings(CreateNodePool(settings))
                .Authentication(new ApiKey(details.ApiKey)),
            AuthenticationType.BEARER => new ElasticsearchClientSettings(CreateNodePool(settings))
                .Authentication(new BearerToken(details.Token!)),
            _ => new ElasticsearchClientSettings(CreateNodePool(settings)),
        };
        if (!string.IsNullOrWhiteSpace(settings.CertificateFingerprint))
        {
            // Not the transport's own CertificateFingerprint: it accepts any chain that lists the fingerprint, whatever the
            // chain errors, so a forged leaf sent with the public CA certificate passes.
            clientSettings.ServerCertificateValidationCallback(CertificateFingerprints.Validator(settings.CertificateFingerprint));
        }
        else if (settings.DevelopmentOnlySkipCertificateValidation == true)
        {
            clientSettings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);
        }

        return clientSettings;
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

    /// <summary>Sends <c>Authorization: Bearer &lt;token&gt;</c>, which the transport has no type for.</summary>
    private sealed class BearerToken(string token) : AuthorizationHeader
    {
        public override string AuthScheme => "Bearer";

        public override bool TryGetAuthorizationParameters(out string parameter)
        {
            parameter = token;
            return true;
        }
    }
}
