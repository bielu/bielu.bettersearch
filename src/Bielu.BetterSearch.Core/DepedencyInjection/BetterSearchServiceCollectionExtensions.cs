using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Caching;
using Bielu.BetterSearch.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bielu.BetterSearch.DepedencyInjection;

public static class BetterSearchServiceCollectionExtensions
{
    public static IServiceCollection AddBetterSearch(this IServiceCollection services, IConfiguration configuration, Action<Configurator> configure)
    {
        var configurator = new Configurator(services, configuration);
        configure(configurator);
        var searchConfiguration = configurator.Configuration;
        var defaultProvider = DefaultProvider(searchConfiguration);
        if (defaultProvider is not null)
        {
            searchConfiguration.SearchProviderType = defaultProvider.SearchProviderType;
            searchConfiguration.IndexingProviderType = defaultProvider.IndexingProviderType;
        }

        // Providers register only their own services; the shared ones are registered here, once, after configure has run.
        services.TryAddSingleton(searchConfiguration);
        services.TryAddScoped(typeof(IDocumentValidatorAsync), searchConfiguration.DocumentValidatorType);
        foreach (var provider in searchConfiguration.Providers)
        {
            AddKeyedServices(services, searchConfiguration, provider);
        }

        if (defaultProvider is null && searchConfiguration.Providers.Count > 1)
        {
            var message = NoDefaultProviderMessage(searchConfiguration.Providers);
            services.TryAddScoped<IIndexingServiceAsync>(_ => throw new InvalidOperationException(message));
            services.TryAddScoped<ISearchProviderAsync>(_ => throw new InvalidOperationException(message));
            services.TryAddScoped<IIndexingProviderAsync>(_ => throw new InvalidOperationException(message));
            services.TryAddScoped<ISearchServiceAsync>(_ => throw new InvalidOperationException(message));
        }
        else
        {
            services.TryAddScoped(typeof(IIndexingServiceAsync), searchConfiguration.IndexingServiceType);
            services.TryAddScoped(typeof(ISearchProviderAsync), searchConfiguration.SearchProviderType);
            services.TryAddScoped(typeof(IIndexingProviderAsync), searchConfiguration.IndexingProviderType);
            services.TryAddScoped(typeof(ISearchServiceAsync), searchConfiguration.SearchServiceType);
        }

        if (searchConfiguration.UsesSearchCache)
        {
            // The services take their providers through the interfaces, keyed or not, so decorating the provider
            // registrations puts the cache under every search and indexing service without changing them.
            Decorate<ISearchProviderAsync>(services, (sp, inner) => ActivatorUtilities.CreateInstance<CachingSearchProviderAsyncDecorator>(
                sp, inner, sp.GetRequiredService<ISearchResultCache>()));
            Decorate<IIndexingProviderAsync>(services, (sp, inner) => ActivatorUtilities.CreateInstance<CachingIndexingProviderAsyncDecorator>(
                sp, inner, sp.GetRequiredService<ISearchResultCache>()));
        }

        return services;
    }

    public static IServiceCollection AddBetterSearch(this IServiceCollection services, Action<Configurator> configure)
    {
        var emptyConfiguration = new ConfigurationBuilder().Build();
        return services.AddBetterSearch(emptyConfiguration, configure);
    }

    /// <summary>The provider the unkeyed services use: the one named as default, else the only one, else none.</summary>
    private static ProviderRegistration? DefaultProvider(SearchApplicationConfiguration configuration)
    {
        if (configuration.DefaultProviderKey is { } key)
        {
            return configuration.Providers.FirstOrDefault(provider => string.Equals(provider.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"SetDefaultProvider names '{key}', but no provider is registered under that key. Registered keys: {RegisteredKeys(configuration.Providers)}.");
        }

        return configuration.Providers.Count == 1 ? configuration.Providers[0] : null;
    }

    /// <summary>
    /// Registers the provider, and a validator, an indexing service and a search service built on it, under the provider's key.
    /// A keyed service that is already registered, for example a validator for one key, is kept.
    /// </summary>
    private static void AddKeyedServices(IServiceCollection services, SearchApplicationConfiguration configuration, ProviderRegistration provider)
    {
        var key = provider.Key;
        services.TryAddKeyedScoped(typeof(ISearchProviderAsync), key, provider.SearchProviderType);
        services.TryAddKeyedScoped(typeof(IIndexingProviderAsync), key, provider.IndexingProviderType);
        // The validator of a key defaults to the unkeyed one, so a validator registered once applies to every provider.
        services.TryAddKeyedScoped<IDocumentValidatorAsync>(key, (sp, _) => sp.GetRequiredService<IDocumentValidatorAsync>());
        var indexingServiceType = configuration.IndexingServiceType;
        services.TryAddKeyedScoped<IIndexingServiceAsync>(key, (sp, _) => (IIndexingServiceAsync)ActivatorUtilities.CreateInstance(
            sp,
            indexingServiceType,
            sp.GetRequiredKeyedService<IIndexingProviderAsync>(key),
            sp.GetRequiredKeyedService<IDocumentValidatorAsync>(key)));
        var searchServiceType = configuration.SearchServiceType;
        services.TryAddKeyedScoped<ISearchServiceAsync>(key, (sp, _) => (ISearchServiceAsync)ActivatorUtilities.CreateInstance(
            sp,
            searchServiceType,
            sp.GetRequiredKeyedService<ISearchProviderAsync>(key)));
    }

    /// <summary>
    /// Replaces every registration of <typeparamref name="TService"/>, keyed or not, with one that wraps the service it
    /// used to resolve in <paramref name="decorate"/>, keeping its lifetime. Registrations added later are not decorated.
    /// </summary>
    private static void Decorate<TService>(IServiceCollection services, Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(TService))
            {
                continue;
            }

            services[i] = descriptor.IsKeyedService
                ? new ServiceDescriptor(typeof(TService), descriptor.ServiceKey, (sp, key) => decorate(sp, (TService)(
                    descriptor.KeyedImplementationInstance
                    ?? descriptor.KeyedImplementationFactory?.Invoke(sp, key)
                    ?? ActivatorUtilities.CreateInstance(sp, descriptor.KeyedImplementationType!))), descriptor.Lifetime)
                : new ServiceDescriptor(typeof(TService), sp => decorate(sp, (TService)(
                    descriptor.ImplementationInstance
                    ?? descriptor.ImplementationFactory?.Invoke(sp)
                    ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!))), descriptor.Lifetime);
        }
    }

    private static string NoDefaultProviderMessage(IReadOnlyList<ProviderRegistration> providers)
    {
        var firstKey = providers[0].Key;
        return $"More than one search provider is registered ({RegisteredKeys(providers)}) and none is the default, so the unkeyed BetterSearch services can't be resolved. " +
               $"Resolve them by key, for example [FromKeyedServices(\"{firstKey}\")] ISearchServiceAsync, or choose a default with SetDefaultProvider(\"{firstKey}\") inside AddBetterSearch.";
    }

    private static string RegisteredKeys(IEnumerable<ProviderRegistration> providers) =>
        string.Join(", ", providers.Select(provider => $"'{provider.Key}'"));
}
