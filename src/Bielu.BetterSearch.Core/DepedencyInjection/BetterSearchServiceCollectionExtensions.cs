using Bielu.BetterSearch.Abstractions.Caching;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Caching;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scrutor;

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
            services.TryAddScoped<IIndexLifecycleServiceAsync>(_ => throw new InvalidOperationException(message));
        }
        else
        {
            services.TryAddScoped(typeof(IIndexingServiceAsync), searchConfiguration.IndexingServiceType);
            services.TryAddScoped(typeof(ISearchProviderAsync), searchConfiguration.SearchProviderType);
            services.TryAddScoped(typeof(IIndexingProviderAsync), searchConfiguration.IndexingProviderType);
            services.TryAddScoped(typeof(ISearchServiceAsync), searchConfiguration.SearchServiceType);
            services.TryAddScoped<IIndexLifecycleServiceAsync, IndexLifecycleServiceAsync>();
        }

        if (searchConfiguration.UsesSearchCache)
        {
            AddSearchCacheDecorators(services, defaultProvider?.Key ?? string.Empty);
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
    /// Registers the provider, and a validator, an indexing service, a search service and an index lifecycle service built
    /// on it, under the provider's key.
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
        services.TryAddKeyedScoped<IIndexLifecycleServiceAsync>(key, (sp, _) => new IndexLifecycleServiceAsync(
            sp.GetRequiredKeyedService<IIndexingProviderAsync>(key),
            sp.GetRequiredKeyedService<IDocumentValidatorAsync>(key)));
    }

    /// <summary>
    /// Wraps every <see cref="ISearchServiceAsync"/>, <see cref="IIndexingServiceAsync"/> and
    /// <see cref="IIndexLifecycleServiceAsync"/> registered by now, keyed or not, in the caching or an evicting decorator,
    /// keeping its lifetime. Whatever service type is registered gets the
    /// cache, and a decorator added later, with Scrutor or otherwise, wraps these. The cache key of a keyed search service
    /// holds its provider key, and that of the unkeyed one <paramref name="unkeyedProviderKey"/>, the default provider's.
    /// </summary>
    private static void AddSearchCacheDecorators(IServiceCollection services, string unkeyedProviderKey)
    {
        services.Decorate(new AnyKeyDecoration(typeof(ISearchServiceAsync), (inner, sp, key) =>
            ActivatorUtilities.CreateInstance<CachingSearchServiceAsyncDecorator>(sp, inner, key ?? unkeyedProviderKey, sp.GetRequiredService<ISearchResultCache>())));
        services.Decorate(new AnyKeyDecoration(typeof(IIndexingServiceAsync), (inner, sp, _) =>
            ActivatorUtilities.CreateInstance<CachingIndexingServiceAsyncDecorator>(sp, inner, sp.GetRequiredService<ISearchResultCache>())));
        services.Decorate(new AnyKeyDecoration(typeof(IIndexLifecycleServiceAsync), (inner, sp, _) =>
            ActivatorUtilities.CreateInstance<CachingIndexLifecycleServiceAsyncDecorator>(sp, inner, sp.GetRequiredService<ISearchResultCache>())));
    }

    /// <summary>
    /// A Scrutor decoration of every registration of a service type, unkeyed or under a string key, whose decorator is
    /// given the key it was resolved with. Scrutor's own decorations match one key only.
    /// </summary>
    private sealed class AnyKeyDecoration(Type serviceType, Func<object, IServiceProvider, string?, object> decorate)
        : DecorationStrategy(serviceType, serviceKey: null)
    {
        public override bool CanDecorate(ServiceDescriptor descriptor) =>
            descriptor.ServiceType == ServiceType && descriptor.ServiceKey is null or string;

        protected override bool CanDecorate(Type serviceType) => serviceType == ServiceType;

        public override Func<IServiceProvider, object?, object> CreateDecorator(Type serviceType, string serviceKey) =>
            (sp, key) => decorate(sp.GetRequiredKeyedService(serviceType, serviceKey), sp, key as string);
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
