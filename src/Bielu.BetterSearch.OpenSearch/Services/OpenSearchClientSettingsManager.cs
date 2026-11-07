using Bielu.BetterSearch.OpenSearch.Configuration;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchClientSettingsManager(IOptionsMonitor<OpenSearchOptions> optionsMonitor)
    : IOpenSearchClientSettingsManager
{
    public ConnectionSettings GetOrCreateClientSettings(string indexName)
    {
        var options = optionsMonitor.CurrentValue;
        var shared = options.SharedIndexSettings;
        if (options.Indexes == null || !options.Indexes.TryGetValue(indexName, out var indexSpecific))
        {
            return ParseSettings(shared);
        }

        return ParseSettings(MergeSettings(indexSpecific, shared));
    }

    /// <summary>
    /// Builds a new settings object, leaving the live options untouched: the index's connection string and
    /// authentication win, and an index without its own takes the shared one.
    /// </summary>
    private static OpenSearchIndexSettings MergeSettings(OpenSearchIndexSettings indexSpecific,
        OpenSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType != OpenSearchAuthenticationType.NONE;
        return new OpenSearchIndexSettings
        {
            ConnectionString = string.IsNullOrWhiteSpace(indexSpecific.ConnectionString)
                ? shared.ConnectionString
                : indexSpecific.ConnectionString,
            AuthenticationType = ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
        };
    }

    private static ConnectionSettings ParseSettings(OpenSearchIndexSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new ArgumentException("OpenSearch connection string is missing");
        }

        var connectionSettings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri(settings.ConnectionString)));
        switch (settings.AuthenticationType)
        {
            case OpenSearchAuthenticationType.NONE:
                break;
            case OpenSearchAuthenticationType.PASSWORD:
                var passwordDetails = settings.AuthenticationDetails
                                      ?? throw new ArgumentException("Authentication details are missing");
                connectionSettings.BasicAuthentication(passwordDetails.Username, passwordDetails.Password);
                break;
            case OpenSearchAuthenticationType.APIKEY:
                var apiKeyDetails = settings.AuthenticationDetails
                                    ?? throw new ArgumentException("Authentication details are missing");
                // OpenSearch API keys are sent as they are, "Authorization: ApiKey <token>", not encoded with an id.
                connectionSettings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKeyDetails.ApiKey));
                break;
            default:
                throw new InvalidOperationException("Invalid authentication type");
        }

        return connectionSettings;
    }
}
