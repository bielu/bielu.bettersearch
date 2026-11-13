using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddElasticSearch());

        // Startup validation reads the options before the host starts its hosted services, so the options
        // start the server themselves when it is not running yet.
        services.AddSingleton<ElasticsearchResource>();
        services.AddHostedService(sp => sp.GetRequiredService<ElasticsearchResource>());
        services.AddOptions<ElasticSearchOptions>()
            .Configure<ElasticsearchResource>((options, elasticsearch) =>
                options.SharedIndexSettings = new ElasticSearchIndexSettings
                {
                    ConnectionString = elasticsearch.EnsureStarted().ToString(),
                    AuthenticationType = AuthenticationType.NONE,
                });
    }
}
