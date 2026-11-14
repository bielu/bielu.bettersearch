using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddOpenSearch());

        // Startup validation reads the options before the host starts its hosted services, so the options
        // start the server themselves when it is not running yet.
        services.AddSingleton<OpenSearchResource>();
        services.AddHostedService(sp => sp.GetRequiredService<OpenSearchResource>());
        services.AddOptions<OpenSearchOptions>()
            .Configure<OpenSearchResource>((options, openSearch) =>
                options.SharedIndexSettings = new OpenSearchIndexSettings
                {
                    ConnectionString = openSearch.EnsureStarted().ToString(),
                    AuthenticationType = OpenSearchAuthenticationType.NONE,
                    // Facet fields are text, so the facet tests show that facets count the keyword sub-field.
                    Mappings =
                    [
                        new OpenSearchFieldMapping { Field = FacetsTestBase.Color, Type = OpenSearchFieldType.Text },
                        new OpenSearchFieldMapping { Field = FacetsTestBase.Brand, Type = OpenSearchFieldType.Text }
                    ]
                });
    }
}
