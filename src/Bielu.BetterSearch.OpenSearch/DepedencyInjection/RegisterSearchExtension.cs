using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.OpenSearch.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static Configurator AddOpenSearch(this Configurator configurator)
    {
        configurator.Services.AddSingleton<IOpenSearchClientSettingsManager, OpenSearchClientSettingsManager>();
        configurator.Services.AddScoped<IClientFactoryAsync<IOpenSearchClient>, OpenSearchClientFactory>();
        configurator.Services.AddScoped(typeof(IDocumentValidatorAsync),
            configurator.Configuration.DocumentValidatorType);
        configurator.SetSearchProviderType<OpenSearchSearchProviderAsync>()
            .SetIndexingProviderType<OpenSearchIndexingProviderAsync>();
        configurator.Services.AddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);

        configurator.Services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        configurator.Services.AddScoped<OpenSearchQueryTranslateService>();
        configurator.Services.AddScoped<IQueryTranslateServiceAsync<QueryContainer>>(sp =>
            sp.GetRequiredService<OpenSearchQueryTranslateService>());
        configurator.Services.AddScoped<IPostFilterTranslateServiceAsync<QueryContainer>>(sp =>
            sp.GetRequiredService<OpenSearchQueryTranslateService>());
        configurator.Services.AddScoped<IResultMapper<ISearchResponse<SearchDocument>>, OpenSearchResultMapper>();

        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, LuceneSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<QueryContainer>, SpatialSearchSubQueryTranslator>();
        configurator.Services.Configure<OpenSearchOptions>(
            configurator.AppConfiguration.GetSection(OpenSearchOptions.OpenSearchOptionsKey));
        return configurator;
    }
}
