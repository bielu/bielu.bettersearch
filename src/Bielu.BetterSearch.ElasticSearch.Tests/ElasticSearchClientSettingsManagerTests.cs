using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchClientSettingsManagerTests
{
    private static IElasticsearchClientSettings GetSettings(ElasticSearchOptions options, string index)
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options));
        return manager.CreateClientSettings(manager.GetConnectionSettings(index));
    }

    private static ElasticSearchIndexSettings GetConnectionSettings(ElasticSearchOptions options, string index) =>
        new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options)).GetConnectionSettings(index);

    private static ElasticSearchIndexSettings PasswordSettings(string connectionString, string password) => new()
    {
        ConnectionString = connectionString,
        AuthenticationType = AuthenticationType.PASSWORD,
        AuthenticationDetails = new AuthenticationDetails { Username = "elastic", Password = password },
    };

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
            .Get<ElasticSearchOptions>()!;

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://people:9200"));
    }

    [Fact]
    public void GetConnectionSettings_AssignedIndexKeyCasingDiffers_UsesIndexSettings()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["People"] = new() { ConnectionString = "http://people:9200" },
            },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://people:9200"));
    }

    [Fact]
    public void GetConnectionSettings_NoIndexSettings_UsesSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }

    [Fact]
    public void GetConnectionSettings_IndexWithoutConnectionString_LeavesOptionsUnchanged()
    {
        var people = new ElasticSearchIndexSettings { IndexName = "people" };
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = people },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));

        people.ConnectionString.ShouldBeNull();
    }

    [Fact]
    public void GetConnectionSettings_IndexNamedDefault_OtherIndexesUseSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["default"] = new() { ConnectionString = "http://other:9200" },
            },
        };

        GetConnectionSettings(options, "people").ConnectionString.ShouldBe("http://shared:9200");
        GetConnectionSettings(options, "default").ConnectionString.ShouldBe("http://other:9200");
    }

    [Fact]
    public void GetConnectionSettings_IndexWithoutConnectionStringOrAuthentication_InheritsSharedAuthentication()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { IndexName = "people" } },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://shared:9200");
        settings.AuthenticationType.ShouldBe(AuthenticationType.PASSWORD);
        settings.AuthenticationDetails.ShouldNotBeNull().Password.ShouldBe("secret");
    }

    [Fact]
    public void GetConnectionSettings_BulkBatchSize_IndexValueWinsAndAnUnsetIndexTakesTheSharedOne()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new() { BulkBatchSize = 500 },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { BulkBatchSize = 50 },
                ["places"] = new() { IndexName = "places" },
            },
        };

        GetConnectionSettings(options, "people").BulkBatchSize.ShouldBe(50);
        GetConnectionSettings(options, "places").BulkBatchSize.ShouldBe(500);
        GetConnectionSettings(options, "other").BulkBatchSize.ShouldBe(500);
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnConnectionStringOnly_InheritsSharedAuthentication()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { ConnectionString = "https://people:9200" } },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://people:9200");
        settings.AuthenticationType.ShouldBe(AuthenticationType.PASSWORD);
        settings.AuthenticationDetails.ShouldNotBeNull().Password.ShouldBe("secret");
    }

    [Fact]
    public void GetConnectionSettings_IndexWithAuthenticationNone_TurnsSharedAuthenticationOff()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { AuthenticationType = AuthenticationType.NONE },
            },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://shared:9200");
        settings.AuthenticationType.ShouldBe(AuthenticationType.NONE);
        settings.AuthenticationDetails.ShouldBeNull();
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnAuthentication_UsesItsOwn()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = PasswordSettings("https://shared:9200", "shared-secret"),
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = PasswordSettings("https://people:9200", "people-secret"),
            },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("https://people:9200");
        settings.AuthenticationDetails.ShouldNotBeNull().Password.ShouldBe("people-secret");
    }

    [Fact]
    public void GetConnectionSettings_NoSharedSettingsAndIndexHasItsOwn_UsesIndexSettingsWithoutMappings()
    {
        var options = new ElasticSearchOptions
        {
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["people"] = new() { ConnectionString = "http://people:9200", AuthenticationType = AuthenticationType.NONE },
            },
        };

        var settings = GetConnectionSettings(options, "people");

        settings.ConnectionString.ShouldBe("http://people:9200");
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
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new() { ConnectionString = "http://shared:9200", WriteVisibility = shared },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { WriteVisibility = own } },
        };

        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options));

        manager.GetConnectionSettings("people").WriteVisibility.ShouldBe(expected);
        manager.GetConnectionSettings("other").WriteVisibility.ShouldBe(shared ?? WriteVisibility.Default);
    }

    [Theory]
    [InlineData(null, "people", "people")]
    [InlineData("", "people", "people")]
    [InlineData("app-", "people", "app-people")]
    public void GetIndexName_Prefix_IsPutBeforeTheName(string? prefix, string index, string expected)
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(new ElasticSearchOptions { IndexPrefix = prefix }));

        manager.GetIndexName(index).ShouldBe(expected);
    }

    [Fact]
    public void GetIndexName_PrefixChanges_UsesTheCurrentPrefix()
    {
        var monitor = new TestOptionsMonitor<ElasticSearchOptions>(new ElasticSearchOptions { IndexPrefix = "blue-" });
        IElasticSearchClientSettingsManager manager = new ElasticSearchClientSettingsManager(monitor);

        monitor.CurrentValue = new ElasticSearchOptions { IndexPrefix = "green-" };

        manager.GetIndexName("people").ShouldBe("green-people");
    }

    private static ElasticSearchOptions NodeOptions(NodePoolType? poolType, params string[] additionalNodes) => new()
    {
        SharedIndexSettings = new ElasticSearchIndexSettings
        {
            ConnectionString = "http://first:9200",
            AdditionalNodes = additionalNodes,
            PoolType = poolType,
        },
    };

    private static Uri[] NodeUris(IElasticsearchClientSettings settings) => settings.NodePool.Nodes.Select(node => node.Uri).ToArray();

    [Fact]
    public void CreateClientSettings_OnlyConnectionString_UsesSingleNodePool()
    {
        var options = new ElasticSearchOptions { SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://first:9200" } };

        var settings = GetSettings(options, "people");

        settings.NodePool.ShouldBeOfType<SingleNodePool>();
        NodeUris(settings).ShouldBe([new Uri("http://first:9200")]);
    }

    [Fact]
    public void CreateClientSettings_AdditionalNodesWithoutPoolType_UsesStaticPoolWithEveryNode()
    {
        var settings = GetSettings(NodeOptions(null, "http://second:9200", "http://third:9201"), "people");

        settings.NodePool.ShouldBeOfType<StaticNodePool>();
        // A static pool starts from a random node, so the order is not checked.
        NodeUris(settings).ShouldBe([new Uri("http://first:9200"), new Uri("http://second:9200"), new Uri("http://third:9201")], ignoreOrder: true);
    }

    [Theory]
    [InlineData(NodePoolType.Static, typeof(StaticNodePool))]
    [InlineData(NodePoolType.Sticky, typeof(StickyNodePool))]
    [InlineData(NodePoolType.Sniffing, typeof(SniffingNodePool))]
    public void CreateClientSettings_PoolType_UsesThatPoolWithEveryNode(NodePoolType poolType, Type expected)
    {
        var settings = GetSettings(NodeOptions(poolType, "http://second:9200"), "people");

        settings.NodePool.GetType().ShouldBe(expected);
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

        settings.NodePool.ShouldBeOfType<StickyNodePool>();
        NodeUris(settings).ShouldBe([new Uri("http://first:9200")]);
    }

    [Fact]
    public void CreateClientSettings_InvalidAdditionalNode_ThrowsNamingItsPosition()
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(NodeOptions(null, "http://second:9200", "third:9200")));

        var error = Should.Throw<ArgumentException>(() => manager.CreateClientSettings(manager.GetConnectionSettings("people")));

        error.Message.ShouldStartWith($"AdditionalNodes:1 is not an http or https URL. Set {ElasticSearchOptions.ElasticSearchOptionsKey}:SharedIndexSettings:AdditionalNodes:1");
    }

    [Fact]
    public void CreateClientSettings_AdditionalNodeWithAnotherScheme_ThrowsNamingItsPosition()
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(NodeOptions(null, "http://second:9200", "https://third:9200")));

        var error = Should.Throw<ArgumentException>(() => manager.CreateClientSettings(manager.GetConnectionSettings("people")));

        error.Message.ShouldBe($"AdditionalNodes:1 does not use http, as ConnectionString does, and every node must use the same scheme. Set {ElasticSearchOptions.ElasticSearchOptionsKey}:SharedIndexSettings:AdditionalNodes:1, or {ElasticSearchOptions.ElasticSearchOptionsKey}:Indexes:<index>:AdditionalNodes:1 for one index.");
    }

    [Fact]
    public void CreateClientSettings_UndefinedPoolType_ThrowsNamingTheSetting()
    {
        var manager = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(NodeOptions((NodePoolType)42)));

        var error = Should.Throw<ArgumentException>(() => manager.CreateClientSettings(manager.GetConnectionSettings("people")));

        error.Message.ShouldStartWith("PoolType 42 is not one of Static, Sticky, Sniffing.");
    }

    [Fact]
    public void GetConnectionSettings_IndexWithoutConnectionString_TakesSharedNodesAndPoolType()
    {
        var options = NodeOptions(NodePoolType.Sticky, "http://second:9200");
        options.Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { BulkBatchSize = 10 } };

        var settings = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options)).GetConnectionSettings("people");

        settings.ConnectionString.ShouldBe("http://first:9200");
        settings.AdditionalNodes.ShouldBe(["http://second:9200"]);
        settings.PoolType.ShouldBe(NodePoolType.Sticky);
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnNodesAndPoolType_UsesItsOwn()
    {
        var options = NodeOptions(NodePoolType.Sticky, "http://second:9200");
        options.Indexes = new Dictionary<string, ElasticSearchIndexSettings>
        {
            ["people"] = new() { AdditionalNodes = ["http://people:9200"], PoolType = NodePoolType.Sniffing },
        };

        var settings = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options)).GetConnectionSettings("people");

        settings.ConnectionString.ShouldBe("http://first:9200");
        settings.AdditionalNodes.ShouldBe(["http://people:9200"]);
        settings.PoolType.ShouldBe(NodePoolType.Sniffing);
    }

    [Fact]
    public void GetConnectionSettings_IndexWithOwnConnectionString_DoesNotTakeSharedNodes()
    {
        var options = NodeOptions(NodePoolType.Sticky, "http://second:9200");
        options.Indexes = new Dictionary<string, ElasticSearchIndexSettings> { ["people"] = new() { ConnectionString = "http://people:9200" } };

        var settings = new ElasticSearchClientSettingsManager(new TestOptionsMonitor<ElasticSearchOptions>(options)).GetConnectionSettings("people");

        settings.ConnectionString.ShouldBe("http://people:9200");
        settings.AdditionalNodes.ShouldBeNull();
        settings.PoolType.ShouldBe(NodePoolType.Sticky);
        GetSettings(options, "people").NodePool.Nodes.Select(node => node.Uri).ShouldBe([new Uri("http://people:9200")]);
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
            .Get<ElasticSearchOptions>()!;

        var settings = GetSettings(options, "people");

        settings.NodePool.ShouldBeOfType<StickyNodePool>();
        NodeUris(settings).ShouldBe([new Uri("http://first:9200"), new Uri("http://second:9200"), new Uri("http://third:9200")]);
    }

    [Fact]
    public void CreateClientSettings_CloudWithAdditionalNodes_UsesCloudPool()
    {
        var options = NodeOptions(NodePoolType.Sticky, "not a url");
        options.SharedIndexSettings.AuthenticationType = AuthenticationType.CLOUDAPI;
        options.SharedIndexSettings.AuthenticationDetails = new AuthenticationDetails
        {
            // "name:" followed by base64 of "<domain>$<elasticsearch id>$<kibana id>", the format of an Elastic Cloud id.
            Id = "name:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("cloud.example.com$abc123$kibana456")),
            ApiKey = "key",
        };

        var settings = GetSettings(options, "people");

        settings.NodePool.ShouldBeOfType<CloudNodePool>();
    }
}
