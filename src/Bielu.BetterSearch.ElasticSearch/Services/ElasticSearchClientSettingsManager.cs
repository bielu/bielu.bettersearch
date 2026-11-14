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
    /// Builds a new settings object, leaving the live options untouched: the index's connection string and
    /// authentication win, and an index without its own takes the shared one. The TLS settings come from where the
    /// endpoint does: the index's own when it sets a connection string or cloud authentication, else the shared ones. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, and the index's write visibility wins over the shared one.
    /// </summary>
    internal static ElasticSearchIndexSettings MergeSettings(ElasticSearchIndexSettings indexSpecific,
        ElasticSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        // The TLS settings belong to the server, so they come with the endpoint: the connection string, or the cloud id.
        var tls = HasOwnEndpoint(indexSpecific) ? indexSpecific : shared;
        return new ElasticSearchIndexSettings
        {
            ConnectionString = string.IsNullOrWhiteSpace(indexSpecific.ConnectionString) ? shared.ConnectionString : indexSpecific.ConnectionString,
            CertificateFingerprint = tls.CertificateFingerprint,
            DevelopmentOnlySkipCertificateValidation = tls.DevelopmentOnlySkipCertificateValidation ?? false,
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? AuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            Mappings = FieldMappings.Merge(shared.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared.WriteVisibility ?? WriteVisibility.Default,
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
    /// <c>CLOUDAPI</c> don't need a connection string. The options validator runs the same checks at startup.
    /// </summary>
    /// <remarks><c>index</c> is the <c>Indexes</c> entry the settings come from, or <c>null</c> when that is not known or they are the shared settings.</remarks>
    internal static IEnumerable<string> ConnectionFailures(ElasticSearchIndexSettings settings, string? index)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var authenticationType = settings.AuthenticationType ?? AuthenticationType.NONE;
        var details = settings.AuthenticationDetails;
        string?[] failures = authenticationType switch
        {
            AuthenticationType.NONE => [ConnectionSettingsChecks.HttpUrlFailure(key, index, settings.ConnectionString)],
            AuthenticationType.PASSWORD =>
            [
                ConnectionSettingsChecks.HttpUrlFailure(key, index, settings.ConnectionString),
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
            AuthenticationType.APIKEY =>
            [
                ConnectionSettingsChecks.HttpUrlFailure(key, index, settings.ConnectionString),
                ConnectionSettingsChecks.CredentialsFailure(key, index, authenticationType, details,
                    (nameof(AuthenticationDetails.ApiKey), details?.ApiKey)),
            ],
            AuthenticationType.BEARER =>
            [
                ConnectionSettingsChecks.HttpUrlFailure(key, index, settings.ConnectionString),
                ConnectionSettingsChecks.CredentialsFailure(key, index, authenticationType, details,
                    (nameof(AuthenticationDetails.Token), details?.Token)),
            ],
            _ => [ConnectionSettingsChecks.UnknownAuthenticationTypeFailure(key, index, settings.AuthenticationType)],
        };
        return failures.OfType<string>()
            .Concat(ConnectionSettingsChecks.TlsFailures(key, index, settings.CertificateFingerprint, settings.DevelopmentOnlySkipCertificateValidation));
    }

    /// <summary>
    /// Builds the client settings, throwing an <see cref="ArgumentException"/> that names each setting to fix when
    /// <see cref="ConnectionFailures"/> finds any.
    /// </summary>
    private static ElasticsearchClientSettings ParseMergedSettings(ElasticSearchIndexSettings settings)
    {
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, null));
        var details = settings.AuthenticationDetails!;
        var clientSettings = (settings.AuthenticationType ?? AuthenticationType.NONE) switch
        {
            AuthenticationType.PASSWORD => new ElasticsearchClientSettings(new SingleNodePool(new Uri(settings.ConnectionString)))
                .Authentication(new BasicAuthentication(details.Username, details.Password)),
            AuthenticationType.CLOUD => new ElasticsearchClientSettings(new CloudNodePool(details.Id, new BasicAuthentication(details.Username, details.Password))),
            AuthenticationType.CLOUDAPI => new ElasticsearchClientSettings(new CloudNodePool(details.Id, new ApiKey(details.ApiKey))),
            AuthenticationType.APIKEY => new ElasticsearchClientSettings(new SingleNodePool(new Uri(settings.ConnectionString)))
                .Authentication(new ApiKey(details.ApiKey)),
            AuthenticationType.BEARER => new ElasticsearchClientSettings(new SingleNodePool(new Uri(settings.ConnectionString)))
                .Authentication(new BearerToken(details.Token!)),
            _ => new ElasticsearchClientSettings(new SingleNodePool(new Uri(settings.ConnectionString))),
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
