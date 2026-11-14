using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.ElasticSearch.Services.Queries;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.DependencyInjection;
namespace Bielu.BetterSearch.ElasticSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    /// <summary>The key AddElasticSearch registers its provider under when no key is given.</summary>
    public const string DefaultKey = "ElasticSearch";

    /// <summary>
    /// Adds the ElasticSearch provider under <paramref name="key"/>, so its services can be resolved with
    /// <c>[FromKeyedServices(key)]</c>; the unkeyed services use it when it is the only provider or the default.
    /// </summary>
    public static Configurator AddElasticSearch(this Configurator configurator, string key = DefaultKey)
    {
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.AddProvider<ElasticSearchSearchProviderAsync, ElasticSearchIndexingProviderAsync>(key);
        configurator.Services.AddSingleton<IElasticSearchClientSettingsManager, ElasticSearchClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<ElasticsearchClient>, ElasticsearchClientFactory>();
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
        OptionsRegistration.BindAndValidateOnStart<ElasticSearchOptions, ElasticSearchOptionsValidator>(
            configurator.Services, configurator.AppConfiguration, ElasticSearchOptions.ElasticSearchOptionsKey);
        return configurator;
    }
}
