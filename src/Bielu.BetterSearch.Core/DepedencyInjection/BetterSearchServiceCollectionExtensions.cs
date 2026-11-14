using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Services;
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

        AddConfiguredRoutes(searchConfiguration, configuration);
        ResolveRouteKeys(searchConfiguration);

        // Providers register only their own services; the shared ones are registered here, once, after configure has run.
        services.TryAddSingleton(searchConfiguration);
        services.TryAddScoped(typeof(IDocumentValidatorAsync), searchConfiguration.DocumentValidatorType);
        foreach (var provider in searchConfiguration.Providers)
        {
            AddKeyedServices(services, searchConfiguration, provider);
        }

        if (searchConfiguration.RouteMap.Count > 0)
        {
            // With routes, the unkeyed services pick the keyed service for each call; the unkeyed providers stay as they are.
            services.TryAddSingleton(new IndexRoutes(
                searchConfiguration.RouteMap,
                defaultProvider?.Key,
                searchConfiguration.Providers.Select(provider => provider.Key).ToList()));
            services.TryAddScoped<IIndexingServiceAsync, RoutingIndexingServiceAsync>();
            services.TryAddScoped<ISearchServiceAsync, RoutingSearchServiceAsync>();
        }

        if (defaultProvider is null && searchConfiguration.Providers.Count > 1)
        {
            var message = NoDefaultProviderMessage(searchConfiguration.Providers);
            services.TryAddScoped<IIndexingServiceAsync>(_ => throw new InvalidOperationException(message));
            services.TryAddScoped<ISearchProviderAsync>(_ => throw new InvalidOperationException(message));
            services.TryAddScoped<IIndexingProviderAsync>(_ => throw new InvalidOperationException(message));
            services.TryAddScoped<ISearchServiceAsync>(_ => throw new InvalidOperationException(message));
            return services;
        }

        services.TryAddScoped(typeof(IIndexingServiceAsync), searchConfiguration.IndexingServiceType);
        services.TryAddScoped(typeof(ISearchProviderAsync), searchConfiguration.SearchProviderType);
        services.TryAddScoped(typeof(IIndexingProviderAsync), searchConfiguration.IndexingProviderType);
        services.TryAddScoped(typeof(ISearchServiceAsync), searchConfiguration.SearchServiceType);
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
    /// Adds the routes of the <see cref="SearchApplicationConfiguration.RoutesSectionKey"/> section, each child an
    /// index name with a provider key as its value. A route set with <see cref="Configurator.RouteIndex"/> wins.
    /// </summary>
    private static void AddConfiguredRoutes(SearchApplicationConfiguration searchConfiguration, IConfiguration configuration)
    {
        foreach (var route in configuration.GetSection(SearchApplicationConfiguration.RoutesSectionKey).GetChildren())
        {
            if (string.IsNullOrWhiteSpace(route.Value))
            {
                throw new InvalidOperationException(
                    $"{SearchApplicationConfiguration.RoutesSectionKey}:{route.Key} has no provider key. Set it to the key of a registered provider.");
            }

            searchConfiguration.RouteMap.TryAdd(route.Key, route.Value);
        }
    }

    /// <summary>Replaces each route's key with the registered key it names, ignoring case, and fails for a key that isn't registered.</summary>
    private static void ResolveRouteKeys(SearchApplicationConfiguration configuration)
    {
        foreach (var (index, key) in configuration.RouteMap.ToList())
        {
            var provider = configuration.Providers.FirstOrDefault(provider => string.Equals(provider.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"The index '{index}' is routed to '{key}', but no provider is registered under that key. Registered keys: {RegisteredKeys(configuration.Providers)}.");
            configuration.RouteMap[index] = provider.Key;
        }
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

    private static string NoDefaultProviderMessage(IReadOnlyList<ProviderRegistration> providers)
    {
        var firstKey = providers[0].Key;
        return $"More than one search provider is registered ({RegisteredKeys(providers)}) and none is the default, so the unkeyed BetterSearch services can't be resolved. " +
               $"Resolve them by key, for example [FromKeyedServices(\"{firstKey}\")] ISearchServiceAsync, or choose a default with SetDefaultProvider(\"{firstKey}\") inside AddBetterSearch.";
    }

    private static string RegisteredKeys(IEnumerable<ProviderRegistration> providers) =>
        string.Join(", ", providers.Select(provider => $"'{provider.Key}'"));
}
