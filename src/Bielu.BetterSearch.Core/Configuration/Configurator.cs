using Bielu.BetterSearch.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Configuration;

public class Configurator(IServiceCollection services)
{
    public IServiceCollection Services => services;

    public SearchApplicationConfiguration Configuration { get; } = SearchApplicationConfiguration.CurrentInstance;
    public Configurator SetSearchServiceType<T>() where T : class, ISearchServiceAsync
    {
        Configuration.SearchServiceType = typeof(T);
        return this;
    }

    public Configurator SetIndexingServiceType<T>() where T : class, IIndexingServiceAsync
    {
        Configuration.IndexingServiceType = typeof(T);
        return this;
    }

    public Configurator SetSearchProviderType<T>() where T : class, ISearchProviderAsync
    {
        Configuration.SearchProviderType = typeof(T);
        return this;
    }

    public Configurator SetIndexingProviderType<T>() where T : class, IIndexingProviderAsync
    {
        Configuration.IndexingProviderType = typeof(T);
        return this;

    }
}
