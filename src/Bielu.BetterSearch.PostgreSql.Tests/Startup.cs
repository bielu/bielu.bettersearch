using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.PostgreSql.Configuration;
using Bielu.BetterSearch.PostgreSql.DepedencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddPostgreSql());

        // Startup validation reads the options before the host starts its hosted services, so the options
        // start the server themselves when it is not running yet.
        services.AddSingleton<PostgreSqlResource>();
        services.AddHostedService(sp => sp.GetRequiredService<PostgreSqlResource>());
        services.AddOptions<PostgreSqlOptions>()
            .Configure<PostgreSqlResource>((options, postgres) =>
            {
                options.SharedIndexSettings = new PostgreSqlIndexSettings { ConnectionString = postgres.EnsureStarted() };
            });
    }
}
