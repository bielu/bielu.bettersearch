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
    /// authentication win, and an index without its own takes the shared one. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, and the index's write visibility wins over the shared one.
    /// </summary>
    private static OpenSearchIndexSettings MergeSettings(OpenSearchIndexSettings indexSpecific,
        OpenSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        return new OpenSearchIndexSettings
        {
            ConnectionString = string.IsNullOrWhiteSpace(indexSpecific.ConnectionString)
                ? shared.ConnectionString
                : indexSpecific.ConnectionString,
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? OpenSearchAuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            Mappings = FieldMappings.Merge(shared?.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared?.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared?.WriteVisibility ?? WriteVisibility.Default,
        };
    }

    public ConnectionSettings CreateClientSettings(OpenSearchIndexSettings settings)
    {
        var key = OpenSearchOptions.OpenSearchOptionsKey;
        var connectionSettings = new ConnectionSettings(new SingleNodeConnectionPool(ConnectionSettingsChecks.HttpUrl(key, settings.ConnectionString)));
        var authenticationType = settings.AuthenticationType ?? OpenSearchAuthenticationType.NONE;
        switch (authenticationType)
        {
            case OpenSearchAuthenticationType.NONE:
                break;
            case OpenSearchAuthenticationType.PASSWORD:
                var passwordDetails = ConnectionSettingsChecks.Details(key, authenticationType, settings.AuthenticationDetails);
                ConnectionSettingsChecks.Credentials(key, authenticationType,
                    (nameof(OpenSearchAuthenticationDetails.Username), passwordDetails.Username),
                    (nameof(OpenSearchAuthenticationDetails.Password), passwordDetails.Password));
                connectionSettings.BasicAuthentication(passwordDetails.Username, passwordDetails.Password);
                break;
            case OpenSearchAuthenticationType.APIKEY:
                var apiKeyDetails = ConnectionSettingsChecks.Details(key, authenticationType, settings.AuthenticationDetails);
                ConnectionSettingsChecks.Credentials(key, authenticationType, (nameof(OpenSearchAuthenticationDetails.ApiKey), apiKeyDetails.ApiKey));
                // OpenSearch API keys are sent as they are, "Authorization: ApiKey <token>", not encoded with an id.
                connectionSettings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKeyDetails.ApiKey));
                break;
            default:
                throw ConnectionSettingsChecks.UnknownAuthenticationType(key, settings.AuthenticationType);
        }

        return connectionSettings;
    }
}
