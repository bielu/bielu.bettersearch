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

    /// <summary>
    /// Builds a new settings object, leaving the live options untouched: the index's connection string and
    /// authentication win, and an index without its own takes the shared one. Mappings are the shared ones with the
    /// index's own on top.
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
        };
    }

    public ConnectionSettings CreateClientSettings(OpenSearchIndexSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new ArgumentException("OpenSearch connection string is missing");
        }

        if (!Uri.TryCreate(settings.ConnectionString, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"OpenSearch connection string {settings.ConnectionString} is not an http or https URL");
        }

        var connectionSettings = new ConnectionSettings(new SingleNodeConnectionPool(uri));
        switch (settings.AuthenticationType ?? OpenSearchAuthenticationType.NONE)
        {
            case OpenSearchAuthenticationType.NONE:
                break;
            case OpenSearchAuthenticationType.PASSWORD:
                var passwordDetails = settings.AuthenticationDetails
                                      ?? throw new ArgumentException("Authentication details are missing");
                if (string.IsNullOrWhiteSpace(passwordDetails.Username) || string.IsNullOrWhiteSpace(passwordDetails.Password))
                {
                    throw new ArgumentException("PASSWORD authentication needs a Username and a Password");
                }

                connectionSettings.BasicAuthentication(passwordDetails.Username, passwordDetails.Password);
                break;
            case OpenSearchAuthenticationType.APIKEY:
                var apiKeyDetails = settings.AuthenticationDetails
                                    ?? throw new ArgumentException("Authentication details are missing");
                if (string.IsNullOrWhiteSpace(apiKeyDetails.ApiKey))
                {
                    throw new ArgumentException("APIKEY authentication needs an ApiKey");
                }

                // OpenSearch API keys are sent as they are, "Authorization: ApiKey <token>", not encoded with an id.
                connectionSettings.ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKeyDetails.ApiKey));
                break;
            default:
                throw new ArgumentException($"Invalid authentication type {settings.AuthenticationType}");
        }

        return connectionSettings;
    }
}
