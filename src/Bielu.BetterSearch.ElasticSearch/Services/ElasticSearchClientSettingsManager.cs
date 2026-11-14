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
        var shared = options.SharedIndexSettings;
        if (options.Indexes == null || !options.Indexes.TryGetValue(indexName, out var indexSpecific))
        {
            return MergeSettings(new ElasticSearchIndexSettings(), shared);
        }

        return MergeSettings(indexSpecific, shared);
    }

    public ElasticsearchClientSettings CreateClientSettings(ElasticSearchIndexSettings settings) =>
        ParseMergedSettings(settings);

    /// <summary>
    /// Builds a new settings object, leaving the live options untouched: the index's connection string and
    /// authentication win, and an index without its own takes the shared one. Mappings are the shared ones with the
    /// index's own on top, the bulk batch size is the index's own or else the shared one, and the index's write visibility wins over the shared one.
    /// </summary>
    private static ElasticSearchIndexSettings MergeSettings(ElasticSearchIndexSettings indexSpecific,
        ElasticSearchIndexSettings shared)
    {
        var ownAuthentication = indexSpecific.AuthenticationType is not null;
        return new ElasticSearchIndexSettings
        {
            ConnectionString = string.IsNullOrWhiteSpace(indexSpecific.ConnectionString)
                ? shared.ConnectionString
                : indexSpecific.ConnectionString,
            AuthenticationType = (ownAuthentication ? indexSpecific.AuthenticationType : shared.AuthenticationType)
                                 ?? AuthenticationType.NONE,
            AuthenticationDetails = ownAuthentication ? indexSpecific.AuthenticationDetails : shared.AuthenticationDetails,
            Mappings = FieldMappings.Merge(shared?.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared?.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared?.WriteVisibility ?? WriteVisibility.Default,
        };
    }

    private static ElasticsearchClientSettings ParseMergedSettings(ElasticSearchIndexSettings finalSearchIndexSettings)
    {
        var settings = new ElasticsearchClientSettings(GetNodePool(finalSearchIndexSettings));
        if (finalSearchIndexSettings.AuthenticationType == AuthenticationType.PASSWORD)
        {
            if (finalSearchIndexSettings.AuthenticationDetails == null)
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
        (connectionString.AuthenticationType ?? AuthenticationType.NONE) switch
        {
            AuthenticationType.NONE => new SingleNodePool(new Uri(connectionString.ConnectionString)),
            AuthenticationType.PASSWORD => new(new Uri(connectionString.ConnectionString)),
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
