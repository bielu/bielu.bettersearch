using Bielu.BetterSearch.ElasticSearch.Configuration;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchClientSettingsManager(IOptionsMonitor<ElasticSearchOptions> optionsMonitor)
    : IElasticSearchClientSettingsManager
{
    public ElasticsearchClientSettings GetOrCreateClientSettings(string indexName)
    {
        var indexSpecific = optionsMonitor.CurrentValue.Indexes?.FirstOrDefault(x => x.Key == indexName);
        var shared = optionsMonitor.CurrentValue.SharedIndexSettings;

        return ParseSettings(indexSpecific?.Value, shared);
    }

    private static ElasticsearchClientSettings ParseSettings(ElasticSearchIndexSettings? indexSpecific,
        ElasticSearchIndexSettings shared)
    {
        if (indexSpecific == null)
        {
            return ParseMergedSettings(shared);
        }

        return ParseMergedSettings(MergeSettings(indexSpecific, shared));
    }

    private static ElasticSearchIndexSettings MergeSettings(ElasticSearchIndexSettings indexSpecific,
        ElasticSearchIndexSettings shared)
    {
        if (string.IsNullOrWhiteSpace(indexSpecific.ConnectionString))
        {
            indexSpecific.ConnectionString = shared.ConnectionString;
        }

        return indexSpecific;
    }

    private static ElasticsearchClientSettings ParseMergedSettings(ElasticSearchIndexSettings finalSearchIndexSettings)
    {
        var settings = new ElasticsearchClientSettings(GetNodePool(finalSearchIndexSettings));
        if (finalSearchIndexSettings.AuthenticationType == AuthenticationType.PASSWORD)
        {
            if(finalSearchIndexSettings.AuthenticationDetails == null)
            {
                throw new ArgumentException("Authentication details are missing");
            }
            settings.Authentication(new BasicAuthentication(finalSearchIndexSettings.AuthenticationDetails.Username,
                finalSearchIndexSettings.AuthenticationDetails.Password));
        }
        return settings;
    }

    private static SingleNodePool GetNodePool(ElasticSearchIndexSettings connectionString) =>
        //todo: figure out how to use StickyNodePool/StaticNodePool
        connectionString.AuthenticationType switch
        {
            AuthenticationType.NONE => new SingleNodePool(new Uri(connectionString.ConnectionString)),
            AuthenticationType.PASSWORD => new (new Uri(connectionString.ConnectionString)),
            AuthenticationType.CLOUD => connectionString.AuthenticationDetails != null
                ? new CloudNodePool(connectionString.AuthenticationDetails.Id,
                    new BasicAuthentication(connectionString.AuthenticationDetails!.Username,
                        connectionString.AuthenticationDetails.Password))
                : throw new ArgumentException("Cloud authentication details are missing"),
            AuthenticationType.CLOUDAPI => connectionString.AuthenticationDetails != null
                ? new CloudNodePool(connectionString.AuthenticationDetails.Id,
                    new ApiKey(connectionString.AuthenticationDetails.ApiKey))
                : throw new ArgumentException("Cloud authentication details are missing"),
            _ => throw new InvalidOperationException("Invalid authentication type")
        };
}
