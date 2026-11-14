using System.Text;
using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticsearchClientFactoryTests
{
    private static ElasticsearchClientFactory CreateFactory(ElasticSearchOptions options) =>
        CreateFactory(new TestOptionsMonitor<ElasticSearchOptions>(options));

    private static ElasticsearchClientFactory CreateFactory(TestOptionsMonitor<ElasticSearchOptions> monitor) =>
        new(new ElasticSearchClientSettingsManager(monitor));

    private static ElasticSearchOptions SharedOptions(string connectionString, string? password = null) => new()
    {
        SharedIndexSettings = password == null
            ? new ElasticSearchIndexSettings { ConnectionString = connectionString }
            : new ElasticSearchIndexSettings
            {
                ConnectionString = connectionString,
                AuthenticationType = AuthenticationType.PASSWORD,
                AuthenticationDetails = new AuthenticationDetails { Username = "elastic", Password = password },
            },
    };

    private static string? AuthorizationParameters(ElasticsearchClient client) =>
        client.ElasticsearchClientSettings.Authentication is { } authentication &&
        authentication.TryGetAuthorizationParameters(out var parameters)
            ? parameters
            : null;

    private static string Basic(string username, string password) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

    private static Uri NodeUri(ElasticsearchClient client) =>
        client.ElasticsearchClientSettings.NodePool.Nodes.Single().Uri;

    [Fact]
    public async Task GetOrCreateClientAsync_IndexWithoutConnectionString_UsesSharedClient()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { IndexName = "people" },
            },
        };
        var factory = CreateFactory(options);

        var people = await factory.GetOrCreateClientAsync("people");
        var other = await factory.GetOrCreateClientAsync("other");

        people.ShouldBeSameAs(other);
        NodeUri(people).ShouldBe(new Uri("http://shared:9200"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexNameCasingDiffers_SharesOneClient()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { ConnectionString = "http://people:9200" },
            },
        };
        var factory = CreateFactory(options);

        var upper = await factory.GetOrCreateClientAsync("People");
        var lower = await factory.GetOrCreateClientAsync("people");

        upper.ShouldBeSameAs(lower);
        NodeUri(upper).ShouldBe(new Uri("http://people:9200"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetOrCreateClientAsync_IndexNamedDefaultWithOwnConnectionString_DoesNotCollideWithSharedClient(bool defaultFirst)
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["default"] = new() { ConnectionString = "http://other:9200" },
            },
        };
        var factory = CreateFactory(options);

        ElasticsearchClient defaultIndex;
        ElasticsearchClient shared;
        if (defaultFirst)
        {
            defaultIndex = await factory.GetOrCreateClientAsync("default");
            shared = await factory.GetOrCreateClientAsync("people");
        }
        else
        {
            shared = await factory.GetOrCreateClientAsync("people");
            defaultIndex = await factory.GetOrCreateClientAsync("default");
        }

        NodeUri(defaultIndex).ShouldBe(new Uri("http://other:9200"));
        NodeUri(shared).ShouldBe(new Uri("http://shared:9200"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_TwoIndexesWithSameConnectionString_ShareOneClient()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { ConnectionString = "http://other:9200" },
                ["places"] = new() { ConnectionString = "http://other:9200" },
            },
        };
        var factory = CreateFactory(options);

        var people = await factory.GetOrCreateClientAsync("people");
        var places = await factory.GetOrCreateClientAsync("places");
        var shared = await factory.GetOrCreateClientAsync("things");

        people.ShouldBeSameAs(places);
        people.ShouldNotBeSameAs(shared);
        NodeUri(people).ShouldBe(new Uri("http://other:9200"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_SharedConnectionStringChanges_UsesNewConnectionString()
    {
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(SharedOptions("http://old:9200"));
        var factory = CreateFactory(monitor);
        var before = await factory.GetOrCreateClientAsync("people");

        monitor.CurrentValue = SharedOptions("http://new:9200");
        var after = await factory.GetOrCreateClientAsync("people");

        NodeUri(before).ShouldBe(new Uri("http://old:9200"));
        NodeUri(after).ShouldBe(new Uri("http://new:9200"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_SharedPasswordChanges_UsesNewPassword()
    {
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(SharedOptions("http://shared:9200", password: "old"));
        var factory = CreateFactory(monitor);
        var before = await factory.GetOrCreateClientAsync("people");

        monitor.CurrentValue = SharedOptions("http://shared:9200", password: "new");
        var after = await factory.GetOrCreateClientAsync("people");

        AuthorizationParameters(before).ShouldBe(Basic("elastic", "old"));
        AuthorizationParameters(after).ShouldBe(Basic("elastic", "new"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexWithoutConnectionStringUnderSharedPassword_UsesSharedAuthentication()
    {
        var options = SharedOptions("http://shared:9200", password: "secret");
        options.Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { IndexName = "people" } };

        var people = await CreateFactory(options).GetOrCreateClientAsync("people");

        NodeUri(people).ShouldBe(new Uri("http://shared:9200"));
        AuthorizationParameters(people).ShouldBe(Basic("elastic", "secret"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexWithoutConnectionStringUnderSharedCloudApiKey_UsesSharedAuthentication()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                AuthenticationType = AuthenticationType.CLOUDAPI,
                AuthenticationDetails = new AuthenticationDetails { Id = CloudId, ApiKey = "shared-key" },
            },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { IndexName = "people" } },
        };

        var people = await CreateFactory(options).GetOrCreateClientAsync("people");

        NodeUri(people).ShouldBe(new Uri("https://abc123.cloud.example.com"));
        AuthorizationParameters(people).ShouldBe("shared-key");
    }

    // "name:" followed by base64 of "<domain>$<elasticsearch id>$<kibana id>", the format of an Elastic Cloud id.
    private static readonly string CloudId =
        "name:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("cloud.example.com$abc123$kibana456"));

    [Fact]
    public async Task TryGetClientAsync_InvalidSharedSettingsFixed_SucceedsOnNextCall()
    {
        var invalid = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = AuthenticationType.PASSWORD,
            },
        };
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(invalid);
        var factory = CreateFactory(monitor);

        var failed = await factory.TryGetClientAsync("people", TestContext.Current.CancellationToken);
        failed.IsFailed.ShouldBeTrue();
        failed.Errors.Single().Message.ShouldStartWith("PASSWORD authentication needs AuthenticationDetails.");

        invalid.SharedIndexSettings.AuthenticationDetails = new AuthenticationDetails { Username = "elastic", Password = "secret" };
        var fixedResult = await factory.TryGetClientAsync("people", TestContext.Current.CancellationToken);

        fixedResult.IsSuccess.ShouldBeTrue();
        AuthorizationParameters(fixedResult.Value).ShouldBe(Basic("elastic", "secret"));
    }

    [Fact]
    public async Task TryGetClientAsync_ClientCreationFailedOnce_SucceedsOnNextCallWithSameSettings()
    {
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(SharedOptions("http://shared:9200"));
        var factory = new ElasticsearchClientFactory(new FailingOnceElasticSearchClientSettingsManagerDecorator(new ElasticSearchClientSettingsManager(monitor)));

        var failed = await factory.TryGetClientAsync("people", TestContext.Current.CancellationToken);
        failed.IsFailed.ShouldBeTrue();
        failed.Errors.Single().Message.ShouldBe("first call fails");

        // Same settings, so the same cache entry: a Lazy that cached the exception would fail this call too.
        var retried = await factory.TryGetClientAsync("people", TestContext.Current.CancellationToken);

        retried.IsSuccess.ShouldBeTrue();
        NodeUri(retried.Value).ShouldBe(new Uri("http://shared:9200"));
    }

    private sealed class FailingOnceElasticSearchClientSettingsManagerDecorator(IElasticSearchClientSettingsManager inner) : IElasticSearchClientSettingsManager
    {
        private int _calls;

        public ElasticSearchIndexSettings GetConnectionSettings(string indexName) => inner.GetConnectionSettings(indexName);

        public ElasticsearchClientSettings CreateClientSettings(ElasticSearchIndexSettings settings) =>
            Interlocked.Increment(ref _calls) == 1
                ? throw new InvalidOperationException("first call fails")
                : inner.CreateClientSettings(settings);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_AdditionalNodesOrPoolTypeChange_GetsNewClient()
    {
        ElasticSearchIndexSettings Shared(NodePoolType? poolType, params string[] nodes) =>
            new() { ConnectionString = "http://first:9200", AdditionalNodes = nodes, PoolType = poolType };
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(new ElasticSearchOptions { SharedIndexSettings = Shared(null, "http://second:9200") });
        var factory = CreateFactory(monitor);
        var before = await factory.GetOrCreateClientAsync("people");
        (await factory.GetOrCreateClientAsync("people")).ShouldBeSameAs(before);

        monitor.CurrentValue = new ElasticSearchOptions { SharedIndexSettings = Shared(null, "http://third:9200") };
        var otherNodes = await factory.GetOrCreateClientAsync("people");
        monitor.CurrentValue = new ElasticSearchOptions { SharedIndexSettings = Shared(NodePoolType.Sticky, "http://third:9200") };
        var otherPool = await factory.GetOrCreateClientAsync("people");

        otherNodes.ShouldNotBeSameAs(before);
        otherNodes.ElasticsearchClientSettings.NodePool.Nodes.Select(node => node.Uri).ShouldContain(new Uri("http://third:9200"));
        otherPool.ShouldNotBeSameAs(otherNodes);
        otherPool.ElasticsearchClientSettings.NodePool.ShouldBeOfType<StickyNodePool>();
    }
}
