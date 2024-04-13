using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.Configuration;

public class Configurator(IServiceCollection services)
{
    public IServiceCollection Services => services;

    public SearchApplicationConfiguration Configuration { get; } = SearchApplicationConfiguration.CurrentInstance;

}
