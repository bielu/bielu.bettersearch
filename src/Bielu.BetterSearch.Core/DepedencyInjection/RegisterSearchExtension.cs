using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static IServiceCollection AddBetterSearch(this IServiceCollection services, Action<Configurator> configure)
    {
        var configurator = new Configurator(services);
        configure(configurator);
        services.AddSingleton(configurator.Configuration);
        services.AddScoped(typeof(IDocumentValidatorAsync) ,configurator.Configuration.DocumentValidatorType);
        services.AddScoped(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);
        services.AddScoped(typeof(ISearchProviderAsync), configurator.Configuration.SearchProviderType);
        services.AddScoped(typeof(IIndexingProviderAsync), configurator.Configuration.IndexingProviderType);
        services.AddScoped(typeof(ISearchServiceAsync), configurator.Configuration.SearchServiceType);
        return services;
    }
}
