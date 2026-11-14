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
            Mappings = FieldMappings.Merge(shared.Mappings, indexSpecific.Mappings),
            BulkBatchSize = indexSpecific.BulkBatchSize ?? shared.BulkBatchSize,
            WriteVisibility = indexSpecific.WriteVisibility ?? shared.WriteVisibility ?? WriteVisibility.Default,
        };
    }

    /// <summary>
    /// Checks the settings as OpenSearch does, throwing an <see cref="ArgumentException"/> that names the setting to fix.
    /// Elastic Cloud connects by its cloud id, so <c>CLOUD</c> and <c>CLOUDAPI</c> don't need a connection string.
    /// </summary>
    private static ElasticsearchClientSettings ParseMergedSettings(ElasticSearchIndexSettings settings)
    {
        var key = ElasticSearchOptions.ElasticSearchOptionsKey;
        var authenticationType = settings.AuthenticationType ?? AuthenticationType.NONE;
        switch (authenticationType)
        {
            case AuthenticationType.NONE:
                return new ElasticsearchClientSettings(new SingleNodePool(ConnectionSettingsChecks.HttpUrl(key, settings.ConnectionString)));
            case AuthenticationType.PASSWORD:
                var uri = ConnectionSettingsChecks.HttpUrl(key, settings.ConnectionString);
                var password = ConnectionSettingsChecks.Details(key, authenticationType, settings.AuthenticationDetails);
                ConnectionSettingsChecks.Credentials(key, authenticationType,
                    (nameof(AuthenticationDetails.Username), password.Username), (nameof(AuthenticationDetails.Password), password.Password));
                return new ElasticsearchClientSettings(new SingleNodePool(uri))
                    .Authentication(new BasicAuthentication(password.Username, password.Password));
            case AuthenticationType.CLOUD:
                var cloud = ConnectionSettingsChecks.Details(key, authenticationType, settings.AuthenticationDetails);
                ConnectionSettingsChecks.Credentials(key, authenticationType,
                    (nameof(AuthenticationDetails.Id), cloud.Id), (nameof(AuthenticationDetails.Username), cloud.Username),
                    (nameof(AuthenticationDetails.Password), cloud.Password));
                return new ElasticsearchClientSettings(new CloudNodePool(cloud.Id, new BasicAuthentication(cloud.Username, cloud.Password)));
            case AuthenticationType.CLOUDAPI:
                var cloudApi = ConnectionSettingsChecks.Details(key, authenticationType, settings.AuthenticationDetails);
                ConnectionSettingsChecks.Credentials(key, authenticationType,
                    (nameof(AuthenticationDetails.Id), cloudApi.Id), (nameof(AuthenticationDetails.ApiKey), cloudApi.ApiKey));
                return new ElasticsearchClientSettings(new CloudNodePool(cloudApi.Id, new ApiKey(cloudApi.ApiKey)));
            default:
                throw ConnectionSettingsChecks.UnknownAuthenticationType(key, settings.AuthenticationType);
        }
    }
}
