using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
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
        configurator.Services
            .AddScoped<IQueryTranslateServiceAsync<BoolQueryDescriptor<SearchDocument>>,
                OpenSearchQueryTranslateService>();
        configurator.Services.Configure<OpenSearchOptions>(
            configurator.AppConfiguration.GetSection(OpenSearchOptions.OpenSearchOptionsKey));
        return configurator;
    }
}
