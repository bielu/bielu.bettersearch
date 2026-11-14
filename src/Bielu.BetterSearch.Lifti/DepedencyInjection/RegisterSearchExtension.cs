using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Lifti.Services;
using Bielu.BetterSearch.Lifti.Services.Queries;
using Lifti;
using Lifti.Querying;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    /// <summary>The key AddLiftiSearch registers its provider under when no key is given.</summary>
    public const string DefaultKey = "Lifti";

    /// <summary>
    /// Adds the Lifti provider under <paramref name="key"/>, so its services can be resolved with
    /// <c>[FromKeyedServices(key)]</c>; the unkeyed services use it when it is the only provider or the default.
    /// </summary>
    public static Configurator AddLiftiSearch(this Configurator configurator, string key = DefaultKey)
    {
        // AddBetterSearch registers the validator and the indexing and search services after this runs.
        configurator.AddProvider<LiftiSearchProviderAsync, LiftiIndexingProviderAsync>(key);
        configurator.Services.AddSingleton<ILiftiIndexManager, LiftiIndexManager>();
        configurator.Services.AddScoped(typeof(IClientFactoryAsync<IFullTextIndex<string>>),
            typeof(LiftiClientFactoryAsync));
        configurator.Services.AddScoped<IQueryTranslateServiceAsync<IQuery>, LiftiQueryTranslatorService>();
        configurator.Services.AddScoped<IResultMapper<ISearchResults<string>>, LiftiBaseSearchModelMapper>();

        // Register sub-query translators (supported)
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, BooleanSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, TermSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, TermsSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, FuzzySubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, PrefixSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, PrefixPhraseSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, LuceneSubQueryTranslator>();

        // Register sub-query translators (unsupported - return failure with explanatory message)
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, DateRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, LongRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, NumericRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, StringRangeSubQueryTranslator>();
        configurator.Services.AddScoped<ISubQueryTranslator<IQuery>, SpatialSearchSubQueryTranslator>();

        return configurator;
    }
}
