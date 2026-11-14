using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public class MeilisearchClientSettingsTests
{
    private static MeilisearchClientFactory CreateFactory(MeilisearchOptions options, params Action<HttpClient>[] configureClient) =>
        new(new MeilisearchClientSettingsManager(new TestOptionsMonitor<MeilisearchOptions>(options)),
            configureClient.Select(configure => new ClientConfiguration<HttpClient>(configure)));

    private static MeilisearchOptions Options(TimeSpan? requestTimeout = null) => new()
    {
        SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = "http://shared:7700", RequestTimeout = requestTimeout },
    };

    [Fact]
    public async Task GetOrCreateClientAsync_RequestTimeoutSet_ReachesTheHttpClient()
    {
        var httpClients = new List<HttpClient>();

        await CreateFactory(Options(TimeSpan.FromSeconds(7)), httpClients.Add).GetOrCreateClientAsync("people");

        httpClients.Single().Timeout.ShouldBe(TimeSpan.FromSeconds(7));
        httpClients.Single().BaseAddress.ShouldBe(new Uri("http://shared:7700/"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexesWithDifferentTimeouts_GetTwoClients()
    {
        var options = Options(TimeSpan.FromSeconds(7));
        options.Indexes = new Dictionary<string, MeilisearchIndexSettings>
        {
            ["people"] = new() { RequestTimeout = TimeSpan.FromSeconds(3) },
            ["places"] = new() { ConnectionString = "http://places:7700" },
        };
        var timeouts = new Dictionary<string, TimeSpan>();
        var factory = CreateFactory(options, httpClient => timeouts[httpClient.BaseAddress!.Host] = httpClient.Timeout);

        var people = await factory.GetOrCreateClientAsync("people");
        var other = await factory.GetOrCreateClientAsync("other");
        await factory.GetOrCreateClientAsync("places");

        people.ShouldNotBeSameAs(other);
        (await factory.GetOrCreateClientAsync("other")).ShouldBeSameAs(other);
        // places has its own server but no timeout of its own, so it takes the shared one.
        timeouts.ShouldBe(new Dictionary<string, TimeSpan>
        {
            ["shared"] = TimeSpan.FromSeconds(7),
            ["places"] = TimeSpan.FromSeconds(7),
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_ConfigureClient_OverridesTheOptionsOncePerCachedClient()
    {
        var httpClients = new List<HttpClient>();
        var factory = CreateFactory(Options(TimeSpan.FromSeconds(7)), httpClient =>
        {
            httpClients.Add(httpClient);
            httpClient.Timeout = TimeSpan.FromSeconds(42);
        });

        var first = await factory.GetOrCreateClientAsync("people");
        var second = await factory.GetOrCreateClientAsync("places");

        second.ShouldBeSameAs(first);
        httpClients.Single().Timeout.ShouldBe(TimeSpan.FromSeconds(42));
    }

    [Fact]
    public async Task AddMeilisearch_ConfigureClient_RunsOnTheClientTheFactoryCreates()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:Meilisearch:SharedIndexSettings:ConnectionString"] = "http://shared:7700",
            ["Bielu:BetterSearch:Meilisearch:SharedIndexSettings:RequestTimeout"] = "00:00:07",
        }).Build();
        var timeouts = new List<TimeSpan>();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, search => search.AddMeilisearch(configureClient: httpClient => timeouts.Add(httpClient.Timeout)))
            .BuildServiceProvider();

        await services.GetRequiredService<IClientFactoryAsync<MeilisearchClient>>().GetOrCreateClientAsync("people");

        timeouts.ShouldBe([TimeSpan.FromSeconds(7)]);
    }

    [Fact]
    public void ClientKey_ToString_ShowsTheTimeoutButNotTheKey()
    {
        var key = new MeilisearchClientFactory.ClientKey("http://shared:7700", "secret-key", TimeSpan.FromSeconds(7));

        key.ToString().ShouldBe("http://shared:7700 (00:00:07)");
    }
}
