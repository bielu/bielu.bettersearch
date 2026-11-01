using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Elasticsearch;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class Startup
{
    public static void ConfigureHost(IHostBuilder hostBuilder) =>
        hostBuilder.ConfigureAppConfiguration((_, builder) =>
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true));

    public static void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(context.Configuration, x => x.AddElasticSearch());
        services.PostConfigure<ElasticSearchOptions>(x =>
        {
            x.SharedIndexSettings.AuthenticationType = AuthenticationType.PASSWORD;
            x.SharedIndexSettings.AuthenticationDetails ??= new AuthenticationDetails();
            x.SharedIndexSettings.AuthenticationDetails.Password = ElasticsearchBuilder.DefaultPassword;
            x.SharedIndexSettings.AuthenticationDetails.Username = ElasticsearchBuilder.DefaultUsername;
        });
    }
}
