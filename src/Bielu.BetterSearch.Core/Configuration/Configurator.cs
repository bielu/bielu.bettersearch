using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bielu.BetterSearch.Configuration;

public class Configurator(IServiceCollection services, IConfiguration configuration)
{
    public IServiceCollection Services => services;
    public IConfiguration AppConfiguration => configuration;

    // Each AddBetterSearch call gets its own configuration, so two containers in one process don't share provider types.
    public SearchApplicationConfiguration Configuration { get; } = new();

    public Configurator SetDocumentValidatorType<T>() where T : class, IDocumentValidatorAsync
    {
        Configuration.DocumentValidatorType = typeof(T);
        return this;
    }

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

    /// <summary>
    /// Caches search results in <typeparamref name="TCache"/>, registered as a singleton, and evicts an index's results
    /// on every write through <see cref="IIndexingServiceAsync"/>. Without this, nothing is cached.
    /// </summary>
    public Configurator AddSearchCache<TCache>(Action<SearchCacheOptions>? configure = null)
        where TCache : class, ISearchResultCache
    {
        services.TryAddSingleton<ISearchResultCache, TCache>();
        return ConfigureSearchCache(configure);
    }

    /// <summary>
    /// Caches search results with <see cref="MemorySearchResultCache"/>, in this process only. Use it for an
    /// application that runs as one instance; with several, use a cache with a backplane, such as FusionCache.
    /// </summary>
    public Configurator AddMemorySearchCache(Action<SearchCacheOptions>? configure = null)
    {
        services.AddMemoryCache();
        return AddSearchCache<MemorySearchResultCache>(configure);
    }

    /// <summary>Applies <paramref name="configure"/> to the <see cref="SearchCacheOptions"/>, for cache packages to share.</summary>
    public Configurator ConfigureSearchCache(Action<SearchCacheOptions>? configure = null)
    {
        var options = services.AddOptions<SearchCacheOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        return this;
    }

    public Configurator SetIndexingProviderType<T>() where T : class, IIndexingProviderAsync
    {
        Configuration.IndexingProviderType = typeof(T);
        return this;

    }
}
