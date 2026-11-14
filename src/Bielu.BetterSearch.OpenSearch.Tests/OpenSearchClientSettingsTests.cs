using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchClientSettingsTests
{
    private static OpenSearchClientFactory CreateFactory(OpenSearchOptions options,
        params Action<ConnectionSettings>[] configureClient) =>
        new(new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options)),
            configureClient.Select(configure => new ClientConfiguration<ConnectionSettings>(configure)));

    private static OpenSearchOptions Options(TimeSpan? requestTimeout = null, int? maxRetries = null) => new()
    {
        SharedIndexSettings = new OpenSearchIndexSettings
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

        client.ConnectionSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        client.ConnectionSettings.MaxRetries.ShouldBe(4);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexesWithDifferentTimeouts_GetTwoClients()
    {
        var options = Options(TimeSpan.FromSeconds(7), 4);
        options.Indexes = new Dictionary<string, OpenSearchIndexSettings>
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
        people.ConnectionSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        people.ConnectionSettings.MaxRetries.ShouldBe(4);
        places.ConnectionSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        places.ConnectionSettings.MaxRetries.ShouldBe(1);
        other.ConnectionSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
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
        first.ConnectionSettings.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(42));
    }

    [Fact]
    public async Task AddOpenSearch_ConfigureClient_RunsOnTheClientTheFactoryCreates()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bielu:BetterSearch:OpenSearch:SharedIndexSettings:ConnectionString"] = "http://shared:9200",
        }).Build();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddBetterSearch(configuration, search => search.AddOpenSearch(
                configureClient: settings => settings.GlobalHeaders(new System.Collections.Specialized.NameValueCollection { ["X-Test"] = "on" })))
            .BuildServiceProvider();

        var client = await services.GetRequiredService<IClientFactoryAsync<IOpenSearchClient>>().GetOrCreateClientAsync("people");

        client.ConnectionSettings.Headers!["X-Test"].ShouldBe("on");
    }

    [Fact]
    public void ClientKey_ToString_ShowsTheTimeoutButNotTheCredentials()
    {
        var key = OpenSearchClientFactory.ClientKey.From(new OpenSearchIndexSettings
        {
            ConnectionString = "http://shared:9200",
            AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
            AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret-password" },
            RequestTimeout = TimeSpan.FromSeconds(7),
            MaxRetries = 4,
        });

        key.ToString().ShouldContain("RequestTimeout = 00:00:07");
        key.ToString().ShouldContain("MaxRetries = 4");
        key.ToString().ShouldNotContain("secret-password");
    }
}
