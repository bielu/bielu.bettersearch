using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticsearchClientSettingsTests
{
    private static ElasticsearchClientFactory CreateFactory(ElasticSearchOptions options,
        params Action<ElasticsearchClientSettings>[] configureClient) =>
        new(new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options)),
            configureClient.Select(configure => new ClientConfiguration<ElasticsearchClientSettings>(configure)));

    private static ElasticSearchOptions Options(TimeSpan? requestTimeout = null, int? maxRetries = null) => new()
    {
        SharedIndexSettings = new ElasticSearchIndexSettings
        {
            ConnectionString = "http://shared:9200",
            RequestTimeout = requestTimeout,
            MaxRetries = maxRetries,
        },
    };

    [Fact]
    public async Task GetOrCreateClientAsync_RequestTimeoutAndMaxRetriesSet_ReachTheClientSettings()
    {
        var client = await CreateFactory(Options(TimeSpan.FromSeconds(7), 4)).GetOrCreateClientAsync("people");

        client.ElasticsearchClientSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        client.ElasticsearchClientSettings.MaxRetries.ShouldBe(4);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexesWithDifferentTimeouts_GetTwoClients()
    {
        var options = Options(TimeSpan.FromSeconds(7), 4);
        options.Indexes = new Dictionary<string, ElasticSearchIndexSettings>
        {
            ["people"] = new() { RequestTimeout = TimeSpan.FromSeconds(3) },
            ["places"] = new() { MaxRetries = 1 },
        };
        var factory = CreateFactory(options);

        var people = await factory.GetOrCreateClientAsync("people");
        var places = await factory.GetOrCreateClientAsync("places");
        var other = await factory.GetOrCreateClientAsync("other");

        people.ShouldNotBeSameAs(other);
        places.ShouldNotBeSameAs(other);
        people.ShouldNotBeSameAs(places);
        people.ElasticsearchClientSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        people.ElasticsearchClientSettings.MaxRetries.ShouldBe(4);
        places.ElasticsearchClientSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        places.ElasticsearchClientSettings.MaxRetries.ShouldBe(1);
        other.ElasticsearchClientSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_ConfigureClient_OverridesTheOptionsOncePerCachedClient()
    {
        var calls = 0;
        var factory = CreateFactory(Options(TimeSpan.FromSeconds(7)), settings =>
        {
            calls++;
            settings.RequestTimeout(TimeSpan.FromSeconds(42));
        });

        var first = await factory.GetOrCreateClientAsync("people");
        var second = await factory.GetOrCreateClientAsync("places");

        second.ShouldBeSameAs(first);
        calls.ShouldBe(1);
        first.ElasticsearchClientSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(42));
    }

    [Fact]
    public async Task AddElasticSearch_ConfigureClient_RunsOnTheClientTheFactoryCreates()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:ElasticSearch:SharedIndexSettings:ConnectionString"] = "http://shared:9200",
        }).Build();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, search => search.AddElasticSearch(
                configureClient: settings => settings.GlobalHeaders(new System.Collections.Specialized.NameValueCollection { ["X-Test"] = "on" })))
            .BuildServiceProvider();

        var client = await services.GetRequiredService<IClientFactoryAsync<ElasticsearchClient>>().GetOrCreateClientAsync("people");

        client.ElasticsearchClientSettings.Headers!["X-Test"].ShouldBe("on");
    }

    [Fact]
    public void ClientKey_ToString_ShowsTheTimeoutButNotTheCredentials()
    {
        var key = ElasticsearchClientFactory.ClientKey.From(new ElasticSearchIndexSettings
        {
            ConnectionString = "http://shared:9200",
            AuthenticationType = AuthenticationType.PASSWORD,
            AuthenticationDetails = new AuthenticationDetails { Username = "elastic", Password = "secret-password" },
            RequestTimeout = TimeSpan.FromSeconds(7),
            MaxRetries = 4,
        });

        key.ToString().ShouldContain("RequestTimeout = 00:00:07");
        key.ToString().ShouldContain("MaxRetries = 4");
        key.ToString().ShouldNotContain("secret-password");
    }
}
