using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Lifti.Services;
using Lifti;
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
            typeof(ElasticsearchClientFactoryAsync));
        configurator.Services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        return configurator;
    }
}
