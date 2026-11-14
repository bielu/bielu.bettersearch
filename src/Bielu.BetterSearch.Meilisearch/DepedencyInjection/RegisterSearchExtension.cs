using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Meilisearch.Services.Queries;
using Meilisearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    /// <summary>The key AddMeilisearch registers its provider under when no key is given.</summary>
    public const string DefaultKey = "Meilisearch";

    /// <summary>
    /// Adds the Meilisearch provider under <paramref name="key"/>, so its services can be resolved with
    /// <c>[FromKeyedServices(key)]</c>; the unkeyed services use it when it is the only provider or the default.
    /// </summary>
    public static Configurator AddMeilisearch(this Configurator configurator, string key = DefaultKey)
    {
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.AddProvider<MeilisearchSearchProviderAsync, MeilisearchIndexingProviderAsync>(key);
        configurator.Services.AddSingleton<IMeilisearchClientSettingsManager, MeilisearchClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<MeilisearchClient>, MeilisearchClientFactory>();
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
        // AddBetterSearch can run more than once on a collection, but the options are bound and validated once, whatever the key.
        if (!configurator.Services.Any(service => service.ImplementationType == typeof(MeilisearchOptionsValidator)))
        {
            configurator.Services.AddOptions<MeilisearchOptions>()
                .Bind(configurator.AppConfiguration.GetSection(MeilisearchOptions.MeilisearchOptionsKey))
                .ValidateOnStart();
            configurator.Services.AddSingleton<IValidateOptions<MeilisearchOptions>, MeilisearchOptionsValidator>();
        }
        return configurator;
    }
}
