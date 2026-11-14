using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.Services;
using Bielu.BetterSearch.Typesense.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Typesense.DepedencyInjection;

public static class RegisterSearchExtension
{
    /// <summary>The key AddTypesense registers its provider under when no key is given.</summary>
    public const string DefaultKey = "Typesense";

    /// <summary>
    /// Adds the Typesense provider under <paramref name="key"/>, so its services can be resolved with
    /// <c>[FromKeyedServices(key)]</c>; the unkeyed services use it when it is the only provider or the default.
    /// </summary>
    public static Configurator AddTypesense(this Configurator configurator, string key = DefaultKey)
    {
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.AddProvider<TypesenseSearchProviderAsync, TypesenseIndexingProviderAsync>(key);
        configurator.Services.AddSingleton<ITypesenseClientSettingsManager, TypesenseClientSettingsManager>();
        configurator.Services.AddSingleton<IClientFactoryAsync<TypesenseHttpClientWrapper>, TypesenseClientFactory>();
        // Its constructor is internal, so it is built here rather than by the container.
        configurator.Services.AddSingleton(services => new TypesenseSchema(services.GetRequiredService<IOptionsMonitor<TypesenseOptions>>()));
        configurator.Services
            .AddScoped<IQueryTranslateServiceAsync<TypesenseSearchRequest>, TypesenseQueryTranslateService>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, LuceneSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<TypesenseFilter>, SpatialSearchSubQueryTranslator>();
        configurator.Services.Configure<TypesenseOptions>(
            configurator.AppConfiguration.GetSection(TypesenseOptions.TypesenseOptionsKey));
        return configurator;
    }
}
