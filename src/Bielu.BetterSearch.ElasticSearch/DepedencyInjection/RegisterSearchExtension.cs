using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.ElasticSearch.Services.Queries;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
namespace Bielu.BetterSearch.ElasticSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static Configurator AddElasticSearch(this Configurator configurator)
    {
        configurator.Services.AddSingleton<IElasticSearchClientSettingsManager, ElasticSearchClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<ElasticsearchClient>, ElasticsearchClientFactory>();
        configurator.Services.AddScoped(typeof(IDocumentValidatorAsync),
            configurator.Configuration.DocumentValidatorType);
        configurator.SetSearchProviderType<ElasticSearchSearchProviderAsync>()
            .SetIndexingProviderType<ElasticSearchIndexingProviderAsync>();
        configurator.Services.AddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);

        configurator.Services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        configurator.Services.AddScoped<ElasticSearchQueryTranslateService>();
        configurator.Services.AddScoped<IQueryTranslateServiceAsync<Query>>(sp =>
            sp.GetRequiredService<ElasticSearchQueryTranslateService>());
        configurator.Services.AddScoped<IPostFilterTranslateServiceAsync<Query>>(sp =>
            sp.GetRequiredService<ElasticSearchQueryTranslateService>());
        configurator.Services.AddScoped<IResultMapper<SearchResponse<SearchDocument>>, ElasticSearchResultMapper>();

        configurator.Services.AddScoped<ISubQueryTranslator<Query>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, LuceneSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<Query>, SpatialSearchSubQueryTranslator>();
        configurator.Services.AddOptions<ElasticSearchOptions>()
            .Bind(configurator.AppConfiguration.GetSection(ElasticSearchOptions.ElasticSearchOptionsKey))
            .ValidateOnStart();
        configurator.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ElasticSearchOptions>, ElasticSearchOptionsValidator>());
        return configurator;
    }
}
