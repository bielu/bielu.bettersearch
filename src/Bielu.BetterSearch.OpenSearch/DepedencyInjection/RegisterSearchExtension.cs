using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.OpenSearch.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static Configurator AddOpenSearch(this Configurator configurator)
    {
        configurator.Services.AddSingleton<IOpenSearchClientSettingsManager, OpenSearchClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<IOpenSearchClient>, OpenSearchClientFactory>();
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.SetSearchProviderType<OpenSearchSearchProviderAsync>()
            .SetIndexingProviderType<OpenSearchIndexingProviderAsync>();
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
        configurator.Services.AddOptions<OpenSearchOptions>()
            .Bind(configurator.AppConfiguration.GetSection(OpenSearchOptions.OpenSearchOptionsKey))
            .ValidateOnStart();
        configurator.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<OpenSearchOptions>, OpenSearchOptionsValidator>());
        return configurator;
    }
}
