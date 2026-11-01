using Bielu.BetterSearch.DepedencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bielu.BetterSearch.Lifti.Tests;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services) =>
        services.AddLogging(lb => lb.AddXunitOutput())
            .AddBetterSearch(x => x.AddLiftiSearch());
}
