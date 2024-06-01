using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.AspNetCoreTesting;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class Startup
{
    public static void ConfigureHost(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureWebHost(webHostBuilder => webHostBuilder
            .UseTestServerAndAddDefaultHttpClient()
            .UseStartup<AspNetCoreStartup>());
    }

    private sealed class AspNetCoreStartup
    {
        public static void ConfigureServices(IServiceCollection services) =>
            services.AddLogging(lb => lb.AddXunitOutput())
                .AddBetterSearch(x=>x.AddElasticSearch());

        public static void Configure(IApplicationBuilder app) =>
            app.Run(context =>  context.Response.WriteAsync("Hello, world!"));
    }

}
