using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Meilisearch.Services.Queries;
using Meilisearch;
using Microsoft.Extensions.DependencyInjection;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static Configurator AddMeilisearch(this Configurator configurator)
    {
        configurator.Services.AddSingleton<IMeilisearchClientSettingsManager, MeilisearchClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<MeilisearchClient>, MeilisearchClientFactory>();
        configurator.Services.AddScoped(typeof(IDocumentValidatorAsync),
            configurator.Configuration.DocumentValidatorType);
        configurator.SetSearchProviderType<MeilisearchSearchProviderAsync>()
            .SetIndexingProviderType<MeilisearchIndexingProviderAsync>();
        configurator.Services.AddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);

        configurator.Services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        configurator.Services
            .AddScoped<IQueryTranslateServiceAsync<MeilisearchQuery>, MeilisearchQueryTranslateService>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, LuceneSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<MeilisearchFilter>, SpatialSearchSubQueryTranslator>();
        configurator.Services.Configure<MeilisearchOptions>(
            configurator.AppConfiguration.GetSection(MeilisearchOptions.MeilisearchOptionsKey));
        return configurator;
    }
}
