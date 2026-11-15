using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchConnectionTests
{
    private static ServiceProvider Services(Action<Configurator> configure) => new ServiceCollection()
        .AddLogging()
        .AddBetterSearch(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:OpenSearch:SharedIndexSettings:ConnectionString"] = "http://shared:9200",
            ["Bielu:BetterSearch:OpenSearch:Indexes:places:ConnectionString"] = "http://places:9200",
        }).Build(), configure)
        .BuildServiceProvider();

    private static Task<IOpenSearchClient> ClientAsync(ServiceProvider services, string index) =>
        services.GetRequiredService<IClientFactoryAsync<IOpenSearchClient>>().GetOrCreateClientAsync(index);

    [Fact]
    public async Task UseOpenSearchConnection_BeforeAddOpenSearch_GivesEachClientItsOwnConnection()
    {
        var created = new List<IConnection>();
        await using var services = Services(search => search
            .UseOpenSearchConnection(_ =>
            {
                var connection = new InMemoryConnection();
                created.Add(connection);
                return connection;
            })
            .AddOpenSearch());

        var people = await ClientAsync(services, "people");
        var places = await ClientAsync(services, "places");
        var peopleAgain = await ClientAsync(services, "people");

        created.Count.ShouldBe(2);
        people.ConnectionSettings.Connection.ShouldBeSameAs(created[0]);
        places.ConnectionSettings.Connection.ShouldBeSameAs(created[1]);
        peopleAgain.ShouldBeSameAs(people);
    }

    [Fact]
    public async Task UseOpenSearchConnection_CalledTwice_TheLastCallWins()
    {
        var first = new InMemoryConnection();
        var last = new InMemoryConnection();
        await using var services = Services(search => search
            .AddOpenSearch()
            .UseOpenSearchConnection(_ => first)
            .UseOpenSearchConnection(_ => last));

        var client = await ClientAsync(services, "people");

        client.ConnectionSettings.Connection.ShouldBeSameAs(last);
    }

    [Fact]
    public async Task AddOpenSearch_WithoutUseOpenSearchConnection_UsesTheDefaultHttpConnection()
    {
        await using var services = Services(search => search.AddOpenSearch());

        var client = await ClientAsync(services, "people");

        client.ConnectionSettings.Connection.ShouldBeOfType<HttpConnection>();
    }

    [Fact]
    public void UseOpenSearchConnection_NullCallback_Throws() =>
        Should.Throw<ArgumentNullException>(() => new Configurator(new ServiceCollection(), new ConfigurationBuilder().Build())
            .UseOpenSearchConnection(null!));
}
