using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
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
        configurator.Services.Configure<MeilisearchOptions>(
            configurator.AppConfiguration.GetSection(MeilisearchOptions.MeilisearchOptionsKey));
        return configurator;
    }
}
