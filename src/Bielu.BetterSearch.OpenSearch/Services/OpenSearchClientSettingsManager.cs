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

    private static OpenSearchIndexSettings MergeSettings(OpenSearchIndexSettings indexSpecific,
        OpenSearchIndexSettings shared)
    {
        if (string.IsNullOrWhiteSpace(indexSpecific.ConnectionString))
        {
            indexSpecific.ConnectionString = shared.ConnectionString;
        }

        return indexSpecific;
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
                connectionSettings.ApiKeyAuthentication(apiKeyDetails.Id, apiKeyDetails.ApiKey);
                break;
            default:
                throw new InvalidOperationException("Invalid authentication type");
        }

        return connectionSettings;
    }
}
