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

        // The host starts hosted services before any test runs, and options are bound on first use,
        // so the endpoint is known by the time a test asks for a client.
        services.AddSingleton<MeilisearchResource>();
        services.AddHostedService(sp => sp.GetRequiredService<MeilisearchResource>());
        services.AddOptions<MeilisearchOptions>()
            .Configure<MeilisearchResource>((options, meilisearch) =>
                options.SharedIndexSettings = new MeilisearchIndexSettings
                {
                    ConnectionString = meilisearch.Endpoint!.ToString(),
                    ApiKey = meilisearch.MasterKey,
                });
    }
}
