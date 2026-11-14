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
    /// Builds a new settings object, leaving the live options untouched: the index's connection string and
    /// authentication win, and an index without its own takes the shared one. The TLS settings come from where the
    /// connection string does. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, and the index's write visibility wins over the shared one.
    /// </summary>
    internal static OpenSearchIndexSettings MergeSettings(OpenSearchIndexSettings indexSpecific,
        OpenSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        // The TLS settings belong to the server, so they come with the connection string.
        var connection = string.IsNullOrWhiteSpace(indexSpecific.ConnectionString) ? shared : indexSpecific;
        return new OpenSearchIndexSettings
        {
            ConnectionString = connection?.ConnectionString ?? string.Empty,
            CertificateFingerprint = connection?.CertificateFingerprint,
            DevelopmentOnlySkipCertificateValidation = connection?.DevelopmentOnlySkipCertificateValidation ?? false,
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? OpenSearchAuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            Mappings = FieldMappings.Merge(shared?.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared?.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared?.WriteVisibility ?? WriteVisibility.Default,
        };
    }

    /// <summary>
    /// The connection setting failures of <paramref name="settings"/>, already merged, each naming the setting to fix.
    /// The options validator runs the same checks at startup.
    /// </summary>
    /// <remarks>
    /// <c>connection</c> is where the connection string comes from, and <c>authentication</c> where the authentication
    /// type and details come from, which an <c>Indexes</c> entry takes together.
    /// </remarks>
    internal static IEnumerable<string> ConnectionFailures(OpenSearchIndexSettings settings, SettingSource connection, SettingSource authentication)
    {
        var key = OpenSearchOptions.OpenSearchOptionsKey;
        var authenticationType = settings.AuthenticationType ?? OpenSearchAuthenticationType.NONE;
        var details = settings.AuthenticationDetails;
        string?[] failures =
        [
            ConnectionSettingsChecks.HttpUrlFailure(key, connection, settings.ConnectionString),
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
        return failures.OfType<string>()
            .Concat(ConnectionSettingsChecks.TlsFailures(key, connection, settings.CertificateFingerprint, settings.DevelopmentOnlySkipCertificateValidation));
    }

    public ConnectionSettings CreateClientSettings(OpenSearchIndexSettings settings)
    {
        ConnectionSettingsChecks.ThrowIfAny(ConnectionFailures(settings, SettingSource.Unknown, SettingSource.Unknown));
        var connectionSettings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri(settings.ConnectionString)));
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

        if (!string.IsNullOrWhiteSpace(settings.CertificateFingerprint))
        {
            connectionSettings.ServerCertificateValidationCallback(CertificateFingerprints.Validator(settings.CertificateFingerprint));
        }
        else if (settings.DevelopmentOnlySkipCertificateValidation == true)
        {
            connectionSettings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);
        }

        return connectionSettings;
    }
}
