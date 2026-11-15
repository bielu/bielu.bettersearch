using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DependencyInjection;
using Bielu.BetterSearch.PostgreSql.DependencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.PostgreSql.Tests;

public sealed class PostgreSqlPingTests(ISearchProviderAsync searchProvider) : PingTestBase(searchProvider)
{
    [Fact]
    public async Task PingAsync_NoServerListening_ReturnsFailedResult()
    {
        // Nothing listens on port 1, so the connection is refused.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:PostgreSql:SharedIndexSettings:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=search;Username=app;Password=secret-value;Timeout=2",
        }).Build();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, x => x.AddPostgreSql())
            .BuildServiceProvider();

        var result = await services.GetRequiredService<ISearchProviderAsync>().PingAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Message.ShouldStartWith("PostgreSQL ping failed:");
        result.Errors.Single().Message.ShouldNotContain("secret-value");
    }
}
