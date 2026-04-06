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
    public static Configurator AddLiftiSearch(this Configurator configurator)
    {
        configurator.Services.AddSingleton<ILiftiIndexManager, LiftiIndexManager>();
        configurator.Services.AddScoped(typeof(IDocumentValidatorAsync),
            configurator.Configuration.DocumentValidatorType);
        configurator.SetSearchProviderType<LiftiSearchProviderAsync>()
            .SetIndexingProviderType<LiftiIndexingProviderAsync>();
        configurator.Services.AddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);
        configurator.Services.AddScoped(typeof(IClientFactoryAsync<IFullTextIndex<string>>),
            typeof(LiftiClientFactoryAsync));
        configurator.Services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
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
