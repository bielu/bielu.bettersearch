using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddOpenSearch());

        // The host starts hosted services before any test runs, and options are bound on first use,
        // so the endpoint is known by the time a test asks for a client.
        services.AddSingleton<OpenSearchResource>();
        services.AddHostedService(sp => sp.GetRequiredService<OpenSearchResource>());
        services.AddOptions<OpenSearchOptions>()
            .Configure<OpenSearchResource>((options, openSearch) =>
                options.SharedIndexSettings = new OpenSearchIndexSettings
                {
                    ConnectionString = openSearch.Endpoint!.ToString(),
                    AuthenticationType = OpenSearchAuthenticationType.NONE,
                });
    }
}
