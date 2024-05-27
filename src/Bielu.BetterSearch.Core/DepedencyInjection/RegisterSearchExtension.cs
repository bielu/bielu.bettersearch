﻿using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.DepedencyInjection;

public static class RegisterSearchExtension
{
    public static IServiceCollection AddSearch(this IServiceCollection services, Action<Configurator> configure)
    {
        var configurator = new Configurator(services);
        configure(configurator);
        services.AddSingleton(configurator.Configuration);
        services.AddSingleton(typeof(IDocumentValidatorAsync) ,configurator.Configuration.DocumentValidatorType);
        services.AddSingleton(typeof(IIndexingServiceAsync), configurator.Configuration.IndexingServiceType);
        return services;
    }
}
