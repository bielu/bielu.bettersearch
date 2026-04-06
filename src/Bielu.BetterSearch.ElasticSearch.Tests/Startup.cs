using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Elasticsearch;
using Xunit.DependencyInjection.AspNetCoreTesting;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class Startup
{
    public static void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureWebHost(webHostBuilder => webHostBuilder
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseTestServerAndAddDefaultHttpClient()
            .UseStartup<AspNetCoreStartup>());
    }
    public static void ConfigureServices(IServiceCollection services,HostBuilderContext context)
    {
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(context.Configuration, x => x.AddElasticSearch());
        services.PostConfigure<ElasticSearchOptions>(x =>
        {
            x.SharedIndexSettings.AuthenticationType = AuthenticationType.PASSWORD;
            if (x.SharedIndexSettings.AuthenticationDetails == null)
            {
                x.SharedIndexSettings.AuthenticationDetails = new AuthenticationDetails();
            }

            x.SharedIndexSettings.AuthenticationDetails.Password = ElasticsearchBuilder.DefaultPassword;
            x.SharedIndexSettings.AuthenticationDetails.Username = ElasticsearchBuilder.DefaultUsername;
        });
    }

    private sealed class AspNetCoreStartup
    {

        public static void Configure(IApplicationBuilder app) =>
            app.Run(context =>  context.Response.WriteAsync("Hello, world!"));
    }

}
