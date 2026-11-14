using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.Configuration;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public class OpenSearchClientSettingsManagerTests
{
    private static OpenSearchClientSettingsManager CreateManager(OpenSearchOptions options) =>
        new(new TestOptionsMonitor<OpenSearchOptions>(options));

    private static IConnectionConfigurationValues GetSettings(OpenSearchOptions options, string index = "products")
    {
        var manager = CreateManager(options);
        return manager.CreateClientSettings(manager.GetConnectionSettings(index));
    }

    [Fact]
    public void GetConnectionSettings_BulkBatchSize_IndexValueWinsAndAnUnsetIndexTakesTheSharedOne()
    {
        var manager = CreateManager(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { BulkBatchSize = 500 },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["people"] = new() { BulkBatchSize = 50 },
                ["places"] = new() { IndexName = "places" },
            },
        });

        manager.GetConnectionSettings("people").BulkBatchSize.ShouldBe(50);
        manager.GetConnectionSettings("places").BulkBatchSize.ShouldBe(500);
        manager.GetConnectionSettings("other").BulkBatchSize.ShouldBe(500);
    }

    [Fact]
    public void CreateClientSettings_NoIndexSettings_UsesSharedConnectionString()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
        settings.BasicAuthenticationCredentials.ShouldBeNull();
        settings.ApiKeyAuthenticationCredentials.ShouldBeNull();
    }

    [Fact]
    public void CreateClientSettings_IndexSettingsWithConnectionString_UsesIndexConnectionString()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200" },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
    }

    [Fact]
    public void CreateClientSettings_IndexSettingsWithoutConnectionString_FallsBackToShared()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new()
                {
                    AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                    AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
                },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
        settings.BasicAuthenticationCredentials.Username.ShouldBe("admin");
    }

    [Fact]
    public void CreateClientSettings_ApiKeyAuthentication_SetsApiKeyCredentials()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.APIKEY,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { ApiKey = "os_key" },
            },
        });

        settings.ApiKeyAuthenticationCredentials.ShouldNotBeNull();
        // Sent as "Authorization: ApiKey os_key", the form the OpenSearch security plugin accepts.
        settings.ApiKeyAuthenticationCredentials.Base64EncodedApiKey.CreateString().ShouldBe("os_key");
        settings.BasicAuthenticationCredentials.ShouldBeNull();
    }

    [Fact]
    public void CreateClientSettings_IndexSettingsWithoutAuthentication_UsesSharedAuthentication()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200" },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
        settings.BasicAuthenticationCredentials.ShouldNotBeNull();
        settings.BasicAuthenticationCredentials.Username.ShouldBe("admin");
    }

    [Fact]
    public void CreateClientSettings_IndexSettings_DoesNotChangeTheOptions()
    {
        var products = new OpenSearchIndexSettings();
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["products"] = products },
        };

        GetSettings(options);

        products.ConnectionString.ShouldBeEmpty();
        products.AuthenticationType.ShouldBeNull();
        products.AuthenticationDetails.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreateClientAsync_SameSettings_ReusesOneClient()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200" },
            },
        };
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));

        var people = await factory.GetOrCreateClientAsync("people");
        var orders = await factory.GetOrCreateClientAsync("orders");
        var products = await factory.GetOrCreateClientAsync("products");

        orders.ShouldBeSameAs(people);
        (await factory.GetOrCreateClientAsync("products")).ShouldBeSameAs(products);
        products.ShouldNotBeSameAs(people);
        products.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
    }

    [Fact]
    public void CreateClientSettings_IndexSettingsWithAuthenticationNone_TurnsSharedAuthenticationOff()
    {
        var settings = GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "secret" },
            },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200", AuthenticationType = OpenSearchAuthenticationType.NONE },
            },
        });

        settings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://products:9200"));
        settings.BasicAuthenticationCredentials.ShouldBeNull();
        settings.ApiKeyAuthenticationCredentials.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrCreateClientAsync_IndexEntriesWithoutConnectionSettings_ShareTheSharedClient()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { IndexingEnabled = false },
                ["default"] = new() { ConnectionString = "http://other:9200" },
            },
        };
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));

        var people = await factory.GetOrCreateClientAsync("people");
        var products = await factory.GetOrCreateClientAsync("products");
        var defaultIndex = await factory.GetOrCreateClientAsync("default");

        products.ShouldBeSameAs(people);
        defaultIndex.ShouldNotBeSameAs(people);
        people.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
        defaultIndex.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://other:9200"));
    }

    [Fact]
    public async Task GetOrCreateClientAsync_ChangedSettings_CreatesANewClient()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = OpenSearchAuthenticationType.PASSWORD,
                AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "old" },
            },
        };
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));
        var before = await factory.GetOrCreateClientAsync("people");

        options.SharedIndexSettings.AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "new" };
        var after = await factory.GetOrCreateClientAsync("people");

        after.ShouldNotBeSameAs(before);
        after.ConnectionSettings.BasicAuthenticationCredentials.Password.CreateString().ShouldBe("new");
    }

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD)]
    [InlineData(OpenSearchAuthenticationType.APIKEY)]
    public void ClientKey_ToString_LeavesOutCredentials(OpenSearchAuthenticationType authenticationType)
    {
        var key = OpenSearchClientFactory.ClientKey.From(new OpenSearchIndexSettings
        {
            ConnectionString = "http://shared:9200",
            AuthenticationType = authenticationType,
            AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "s3cret-password", ApiKey = "s3cret-key" },
        });

        var text = key.ToString();

        text.ShouldContain("http://shared:9200");
        text.ShouldContain(authenticationType.ToString());
        text.ShouldNotContain("s3cret");
    }

    [Fact]
    public async Task GetOrCreateClientAsync_InvalidSettingsFixed_CreatesTheClient()
    {
        var options = new OpenSearchOptions();
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(options);
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));
        await Should.ThrowAsync<ArgumentException>(() => factory.GetOrCreateClientAsync("people"));

        options.SharedIndexSettings.ConnectionString = "http://shared:9200";
        var client = await factory.GetOrCreateClientAsync("people");

        client.ConnectionSettings.ConnectionPool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }

    [Theory]
    [InlineData("localhost:9200")]
    [InlineData("not a url")]
    public void CreateClientSettings_ConnectionStringNotAnHttpUrl_Throws(string connectionString) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = connectionString },
        }));

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD)]
    [InlineData(OpenSearchAuthenticationType.APIKEY)]
    public void CreateClientSettings_AuthenticationWithoutDetails_Throws(OpenSearchAuthenticationType authenticationType) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = authenticationType,
            },
        }));

    [Theory]
    [InlineData(OpenSearchAuthenticationType.PASSWORD, "", "s3cret", "")]
    [InlineData(OpenSearchAuthenticationType.PASSWORD, "admin", " ", "")]
    [InlineData(OpenSearchAuthenticationType.APIKEY, "", "", "")]
    [InlineData(OpenSearchAuthenticationType.APIKEY, "", "", "  ")]
    public void CreateClientSettings_AuthenticationWithEmptyCredentials_Throws(
        OpenSearchAuthenticationType authenticationType, string username, string password, string apiKey) =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = "http://shared:9200",
                AuthenticationType = authenticationType,
                AuthenticationDetails = new OpenSearchAuthenticationDetails
                {
                    Username = username,
                    Password = password,
                    ApiKey = apiKey,
                },
            },
        }));

    [Fact]
    public void GetConnectionSettings_BoundIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SharedIndexSettings:ConnectionString"] = "http://shared:9200",
                ["Indexes:People:ConnectionString"] = "http://people:9200",
            })
            .Build()
            .Get<OpenSearchOptions>()!;

        CreateManager(options).GetConnectionSettings("people").ConnectionString.ShouldBe("http://people:9200");
    }

    [Fact]
    public void GetConnectionSettings_AssignedIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["People"] = new() { ConnectionString = "http://people:9200" },
            },
        };

        CreateManager(options).GetConnectionSettings("people").ConnectionString.ShouldBe("http://people:9200");
    }

    [Fact]
    public void CreateClientSettings_MissingConnectionString_Throws() =>
        Should.Throw<ArgumentException>(() => GetSettings(new OpenSearchOptions()));

    [Fact]
    public void GetConnectionSettings_NoSharedSettingsAndIndexHasItsOwn_UsesIndexSettingsWithoutMappings()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = null!,
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["products"] = new() { ConnectionString = "http://products:9200", AuthenticationType = OpenSearchAuthenticationType.NONE },
            },
        };

        var settings = CreateManager(options).GetConnectionSettings("products");

        settings.ConnectionString.ShouldBe("http://products:9200");
        settings.Mappings.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, null, WriteVisibility.Default)]
    [InlineData(WriteVisibility.WaitUntilSearchable, null, WriteVisibility.WaitUntilSearchable)]
    [InlineData(WriteVisibility.WaitUntilSearchable, WriteVisibility.Default, WriteVisibility.Default)]
    [InlineData(null, WriteVisibility.WaitUntilSearchable, WriteVisibility.WaitUntilSearchable)]
    public void GetConnectionSettings_WriteVisibility_IndexSettingWinsAndUnsetInheritsShared(
        WriteVisibility? shared, WriteVisibility? own, WriteVisibility expected)
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new() { ConnectionString = "http://shared:9200", WriteVisibility = shared },
            Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["people"] = new() { WriteVisibility = own } },
        };

        var manager = CreateManager(options);

        manager.GetConnectionSettings("people").WriteVisibility.ShouldBe(expected);
        manager.GetConnectionSettings("other").WriteVisibility.ShouldBe(shared ?? WriteVisibility.Default);
    }

    [Theory]
    [InlineData(null, "people", "people")]
    [InlineData("", "people", "people")]
    [InlineData("app-", "people", "app-people")]
    public void GetIndexName_Prefix_IsPutBeforeTheName(string? prefix, string index, string expected) =>
        CreateManager(new OpenSearchOptions { IndexPrefix = prefix }).GetIndexName(index).ShouldBe(expected);

    private static OpenSearchOptions NodeOptions(NodePoolType? poolType, params string[] additionalNodes) => new()
    {
        SharedIndexSettings = new OpenSearchIndexSettings
        {
            ConnectionString = "http://first:9200",
            AdditionalNodes = additionalNodes,
            PoolType = poolType,
        },
    };

    private static Uri[] NodeUris(IConnectionConfigurationValues settings) => settings.ConnectionPool.Nodes.Select(node => node.Uri).ToArray();

    [Fact]
    public void CreateClientSettings_OnlyConnectionString_UsesSingleNodePool()
    {
        var options = new OpenSearchOptions { SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://first:9200" } };

        var settings = GetSettings(options, "people");

        settings.ConnectionPool.ShouldBeOfType<SingleNodeConnectionPool>();
        NodeUris(settings).ShouldBe([new Uri("http://first:9200")]);
    }

    [Fact]
    public void CreateClientSettings_AdditionalNodesWithoutPoolType_UsesStaticPoolWithEveryNode()
    {
        var settings = GetSettings(NodeOptions(null, "http://second:9200", "http://third:9201"), "people");

        settings.ConnectionPool.ShouldBeOfType<StaticConnectionPool>();
        // A static pool starts from a random node, so the order is not checked.
        NodeUris(settings).ShouldBe([new Uri("http://first:9200"), new Uri("http://second:9200"), new Uri("http://third:9201")], ignoreOrder: true);
    }

    [Theory]
    [InlineData(NodePoolType.Static, typeof(StaticConnectionPool))]
    [InlineData(NodePoolType.Sticky, typeof(StickyConnectionPool))]
    [InlineData(NodePoolType.Sniffing, typeof(SniffingConnectionPool))]
    public void CreateClientSettings_PoolType_UsesThatPoolWithEveryNode(NodePoolType poolType, Type expected)
    {
        var settings = GetSettings(NodeOptions(poolType, "http://second:9200"), "people");

        settings.ConnectionPool.GetType().ShouldBe(expected);
        NodeUris(settings).ShouldBe([new Uri("http://first:9200"), new Uri("http://second:9200")], ignoreOrder: true);
    }

    [Fact]
    public void CreateClientSettings_StickyPoolType_KeepsTheListedOrder()
    {
        var settings = GetSettings(NodeOptions(NodePoolType.Sticky, "http://second:9200", "http://third:9200"), "people");

        NodeUris(settings).ShouldBe([new Uri("http://first:9200"), new Uri("http://second:9200"), new Uri("http://third:9200")]);
    }

    [Fact]
    public void CreateClientSettings_PoolTypeWithOnlyConnectionString_UsesThatPool()
    {
        var settings = GetSettings(NodeOptions(NodePoolType.Sticky), "people");

        settings.ConnectionPool.ShouldBeOfType<StickyConnectionPool>();
        NodeUris(settings).ShouldBe([new Uri("http://first:9200")]);
    }

    [Fact]
    public void CreateClientSettings_InvalidAdditionalNode_ThrowsNamingItsPosition()
    {
        var manager = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(NodeOptions(null, "http://second:9200", "third:9200")));

        var error = Should.Throw<ArgumentException>(() => manager.CreateClientSettings(manager.GetConnectionSettings("people")));

        error.Message.ShouldStartWith($"AdditionalNodes:1 is not an http or https URL. Set {OpenSearchOptions.OpenSearchOptionsKey}:SharedIndexSettings:AdditionalNodes:1");
    }

    [Fact]
    public void CreateClientSettings_AdditionalNodeWithAnotherScheme_ThrowsNamingItsPosition()
    {
        var manager = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(NodeOptions(null, "http://second:9200", "https://third:9200")));

        var error = Should.Throw<ArgumentException>(() => manager.CreateClientSettings(manager.GetConnectionSettings("people")));

        error.Message.ShouldBe($"AdditionalNodes:1 does not use http, as ConnectionString does, and every node must use the same scheme. Set {OpenSearchOptions.OpenSearchOptionsKey}:SharedIndexSettings:AdditionalNodes:1, or {OpenSearchOptions.OpenSearchOptionsKey}:Indexes:<index>:AdditionalNodes:1 for one index.");
    }

    [Fact]
    public void CreateClientSettings_UndefinedPoolType_ThrowsNamingTheSetting()
    {
        var manager = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(NodeOptions((NodePoolType)42)));

        var error = Should.Throw<ArgumentException>(() => manager.CreateClientSettings(manager.GetConnectionSettings("people")));

        error.Message.ShouldStartWith("PoolType 42 is not one of Static, Sticky, Sniffing.");
    }

    [Fact]
    public void GetConnectionSettings_IndexWithoutConnectionString_TakesSharedNodesAndPoolType()
    {
        var options = NodeOptions(NodePoolType.Sticky, "http://second:9200");
        options.Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["people"] = new() { BulkBatchSize = 10 } };

        var settings = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options)).GetConnectionSettings("people");

        settings.ConnectionString.ShouldBe("http://first:9200");
        settings.AdditionalNodes.ShouldBe(["http://second:9200"]);
        settings.PoolType.ShouldBe(NodePoolType.Sticky);
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnNodesAndPoolType_UsesItsOwn()
    {
        var options = NodeOptions(NodePoolType.Sticky, "http://second:9200");
        options.Indexes = new Dictionary<string, OpenSearchIndexSettings>
        {
            ["people"] = new() { AdditionalNodes = ["http://people:9200"], PoolType = NodePoolType.Sniffing },
        };

        var settings = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options)).GetConnectionSettings("people");

        settings.ConnectionString.ShouldBe("http://first:9200");
        settings.AdditionalNodes.ShouldBe(["http://people:9200"]);
        settings.PoolType.ShouldBe(NodePoolType.Sniffing);
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnConnectionString_DoesNotTakeSharedNodes()
    {
        var options = NodeOptions(NodePoolType.Sticky, "http://second:9200");
        options.Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["people"] = new() { ConnectionString = "http://people:9200" } };

        var settings = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options)).GetConnectionSettings("people");

        settings.ConnectionString.ShouldBe("http://people:9200");
        settings.AdditionalNodes.ShouldBeNull();
        settings.PoolType.ShouldBe(NodePoolType.Sticky);
        GetSettings(options, "people").ConnectionPool.Nodes.Select(node => node.Uri).ShouldBe([new Uri("http://people:9200")]);
    }

    [Fact]
    public void GetConnectionSettings_BoundFromConfiguration_ReadsNodesAndPoolType()
    {
        var options = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SharedIndexSettings:ConnectionString"] = "http://first:9200",
                ["SharedIndexSettings:AdditionalNodes:0"] = "http://second:9200",
                ["SharedIndexSettings:AdditionalNodes:1"] = "http://third:9200",
                ["SharedIndexSettings:PoolType"] = "Sticky",
            })
            .Build()
            .Get<OpenSearchOptions>()!;

        var settings = GetSettings(options, "people");

        settings.ConnectionPool.ShouldBeOfType<StickyConnectionPool>();
        NodeUris(settings).ShouldBe([new Uri("http://first:9200"), new Uri("http://second:9200"), new Uri("http://third:9200")]);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_AdditionalNodesOrPoolTypeChange_GetsNewClient()
    {
        OpenSearchIndexSettings Shared(NodePoolType? poolType, params string[] nodes) =>
            new() { ConnectionString = "http://first:9200", AdditionalNodes = nodes, PoolType = poolType };
        var monitor = new TestOptionsMonitor<OpenSearchOptions>(new OpenSearchOptions { SharedIndexSettings = Shared(null, "http://second:9200") });
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(monitor));
        var before = await factory.GetOrCreateClientAsync("people");
        (await factory.GetOrCreateClientAsync("people")).ShouldBeSameAs(before);

        monitor.CurrentValue = new OpenSearchOptions { SharedIndexSettings = Shared(null, "http://third:9200") };
        var otherNodes = await factory.GetOrCreateClientAsync("people");
        monitor.CurrentValue = new OpenSearchOptions { SharedIndexSettings = Shared(NodePoolType.Sticky, "http://third:9200") };
        var otherPool = await factory.GetOrCreateClientAsync("people");

        otherNodes.ShouldNotBeSameAs(before);
        otherNodes.ConnectionSettings.ConnectionPool.Nodes.Select(node => node.Uri).ShouldContain(new Uri("http://third:9200"));
        otherPool.ShouldNotBeSameAs(otherNodes);
        otherPool.ConnectionSettings.ConnectionPool.ShouldBeOfType<StickyConnectionPool>();
    }

    [Theory]
    [InlineData("people")]
    [InlineData("orders")]
    public void GetConnectionSettings_ReturnedNodesChanged_LeavesOptionsUnchanged(string index)
    {
        var options = NodeOptions(NodePoolType.Sticky);
        options.SharedIndexSettings!.AdditionalNodes = ["http://second:9200"];
        options.Indexes = new Dictionary<string, OpenSearchIndexSettings> { ["orders"] = new() { AdditionalNodes = ["http://orders:9200"] } };
        var manager = new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options));

        manager.GetConnectionSettings(index).AdditionalNodes!.Clear();

        options.SharedIndexSettings!.AdditionalNodes.ShouldBe(["http://second:9200"]);
        options.Indexes["orders"].AdditionalNodes.ShouldBe(["http://orders:9200"]);
        manager.GetConnectionSettings(index).AdditionalNodes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetOrCreateClientAsync_NodeListsJoinToSameText_GetsSeparateClients()
    {
        var options = new OpenSearchOptions
        {
            SharedIndexSettings = new OpenSearchIndexSettings { ConnectionString = "http://first:9200" },
            Indexes = new Dictionary<string, OpenSearchIndexSettings>
            {
                ["one"] = new() { AdditionalNodes = ["http://second:9200/a http://third:9200/b"] },
                ["two"] = new() { AdditionalNodes = ["http://second:9200/a", "http://third:9200/b"] },
            },
        };
        var factory = new OpenSearchClientFactory(new OpenSearchClientSettingsManager(new TestOptionsMonitor<OpenSearchOptions>(options)));

        var one = await factory.GetOrCreateClientAsync("one");
        var two = await factory.GetOrCreateClientAsync("two");

        two.ShouldNotBeSameAs(one);
        one.ConnectionSettings.ConnectionPool.Nodes.Count().ShouldBe(2);
        two.ConnectionSettings.ConnectionPool.Nodes.Count().ShouldBe(3);
    }
}
