using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.DepedencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.Typesense.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddTypesense());

        // The host starts hosted services before any test runs, and options are bound on first use,
        // so the endpoint is known by the time a test asks for a client.
        services.AddSingleton<TypesenseResource>();
        services.AddHostedService(sp => sp.GetRequiredService<TypesenseResource>());
        services.AddOptions<TypesenseOptions>()
            .Configure<TypesenseResource>((options, typesense) =>
            {
                options.SharedIndexSettings = new TypesenseIndexSettings
                {
                    ConnectionString = typesense.Endpoint!.ToString(),
                    ApiKey = typesense.ApiKey,
                };
                options.GeoField = "Location";
            });
    }
}
