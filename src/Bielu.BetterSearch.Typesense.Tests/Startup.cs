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

        // Startup validation reads the options before the host starts its hosted services, so the options
        // start the server themselves when it is not running yet.
        services.AddSingleton<TypesenseResource>();
        services.AddHostedService(sp => sp.GetRequiredService<TypesenseResource>());
        services.AddOptions<TypesenseOptions>()
            .Configure<TypesenseResource>((options, typesense) =>
            {
                options.SharedIndexSettings = new TypesenseIndexSettings
                {
                    ConnectionString = typesense.EnsureStarted().ToString(),
                    ApiKey = typesense.ApiKey,
                };
                options.GeoField = "Location";
            });
    }
}
