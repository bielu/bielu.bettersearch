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

        // The host starts hosted services before any test runs, and options are bound on first use,
        // so the connection string is known by the time a test asks for a client.
        services.AddSingleton<ElasticsearchResource>();
        services.AddHostedService(sp => sp.GetRequiredService<ElasticsearchResource>());
        services.AddOptions<ElasticSearchOptions>()
            .Configure<ElasticsearchResource>((options, elasticsearch) =>
            {
                // Aspire reports http://elastic:password@host:port.
                var uri = elasticsearch.ConnectionString!;
                var credentials = uri.UserInfo.Split(':', 2);
                options.SharedIndexSettings = new ElasticSearchIndexSettings
                {
                    ConnectionString = uri.GetLeftPart(UriPartial.Authority).Replace(uri.UserInfo + "@", string.Empty, StringComparison.Ordinal),
                    AuthenticationType = AuthenticationType.PASSWORD,
                    AuthenticationDetails = new AuthenticationDetails
                    {
                        Username = Uri.UnescapeDataString(credentials[0]),
                        Password = Uri.UnescapeDataString(credentials[1]),
                    },
                };
            });
    }
}
