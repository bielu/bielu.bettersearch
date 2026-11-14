using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddMeilisearch());

        // Startup validation reads the options before the host starts its hosted services, so the options
        // start the server themselves when it is not running yet.
        services.AddSingleton<MeilisearchResource>();
        services.AddHostedService(sp => sp.GetRequiredService<MeilisearchResource>());
        services.AddOptions<MeilisearchOptions>()
            .Configure<MeilisearchResource>((options, meilisearch) =>
            {
                options.SharedIndexSettings = new MeilisearchIndexSettings
                {
                    ConnectionString = meilisearch.EnsureStarted().ToString(),
                    ApiKey = meilisearch.MasterKey,
                };
                options.GeoField = "Location";
            });
    }
}
