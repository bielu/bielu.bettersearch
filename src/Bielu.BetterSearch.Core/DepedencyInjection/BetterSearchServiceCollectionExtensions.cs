using Bielu.BetterSearch.Abstractions.Services;
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
        // Providers register only their own services; the shared ones are registered here, once, after configure has run.
        services.TryAddSingleton(configurator.Configuration);
        services.TryAddScoped(typeof(IDocumentValidatorAsync), configurator.Configuration.DocumentValidatorType);
        services.TryAddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);
        services.TryAddScoped(typeof(ISearchProviderAsync), configurator.Configuration.SearchProviderType);
        services.TryAddScoped(typeof(IIndexingProviderAsync), configurator.Configuration.IndexingProviderType);
        services.TryAddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        return services;
    }

    public static IServiceCollection AddBetterSearch(this IServiceCollection services, Action<Configurator> configure)
    {
        var emptyConfiguration = new ConfigurationBuilder().Build();
        return services.AddBetterSearch(emptyConfiguration, configure);
    }
}
