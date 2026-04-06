using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Bielu.BetterSearch.ElasticSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static Configurator AddElasticSearch(this Configurator configurator)
    {
        configurator.Services.AddSingleton<IElasticSearchClientSettingsManager, ElasticSearchClientSettingsManager>();
        configurator.Services.AddScoped<IClientFactoryAsync<ElasticsearchClient>, ElasticsearchClientFactory>();
        configurator.Services.AddScoped(typeof(IDocumentValidatorAsync),
            configurator.Configuration.DocumentValidatorType);
        configurator.SetSearchProviderType<ElasticSearchSearchProviderAsync>()
            .SetIndexingProviderType<ElasticSearchIndexingProviderAsync>();
        configurator.Services.AddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);

        configurator.Services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        configurator.Services
            .AddScoped<IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>>,
                ElasticSearchQueryTranslateService>();
        configurator.Services.Configure<ElasticSearchOptions>(
            configurator.AppConfiguration.GetSection(ElasticSearchOptions.ElasticSearchOptionsKey));
        return configurator;
    }
}
