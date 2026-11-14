using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Configuration;
using Bielu.BetterSearch.Typesense.DepedencyInjection;
using Bielu.BetterSearch.Typesense.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

public sealed class TypesenseClientSettingsTests
{
    private static TypesenseClientFactory CreateFactory(TypesenseOptions options, params Action<HttpClient>[] configureClient)
    {
        var monitor = new TestOptionsMonitor<TypesenseOptions>(options);
        return new TypesenseClientFactory(new TypesenseClientSettingsManager(monitor), monitor,
            configureClient.Select(configure => new ClientConfiguration<HttpClient>(configure)));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_ConfigureClient_RunsAfterTheRequestTimeoutOncePerCachedClient()
    {
        var timeouts = new List<TimeSpan>();
        var options = new TypesenseOptions
        {
            SharedIndexSettings = new TypesenseIndexSettings { ConnectionString = "http://shared:8108" },
            RequestTimeout = TimeSpan.FromSeconds(7),
        };
        using var factory = CreateFactory(options, httpClient => timeouts.Add(httpClient.Timeout));

        var first = await factory.GetOrCreateClientAsync("people");
        var second = await factory.GetOrCreateClientAsync("places");

        second.ShouldBeSameAs(first);
        timeouts.ShouldBe([TimeSpan.FromSeconds(7)]);
    }

    [Fact]
    public async Task AddTypesense_ConfigureClient_RunsOnTheClientTheFactoryCreates()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:Typesense:SharedIndexSettings:ConnectionString"] = "http://shared:8108",
            ["Bielu:BetterSearch:Typesense:RequestTimeout"] = "00:00:07",
        }).Build();
        var timeouts = new List<TimeSpan>();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, search => search.AddTypesense(configureClient: httpClient => timeouts.Add(httpClient.Timeout)))
            .BuildServiceProvider();

        await services.GetRequiredService<IClientFactoryAsync<TypesenseHttpClientWrapper>>().GetOrCreateClientAsync("people");

        timeouts.ShouldBe([TimeSpan.FromSeconds(7)]);
    }
}
