using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchClientSettingsManagerTests
{
    private static IElasticsearchClientSettings GetSettings(ElasticSearchOptions options, string index)
    {
        var manager = new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(options));
        return manager.CreateClientSettings(manager.GetConnectionSettings(index));
    }

    private static ElasticSearchIndexSettings GetConnectionSettings(ElasticSearchOptions options, string index) =>
        new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(options)).GetConnectionSettings(index);

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

        var manager = new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(options));

        manager.GetConnectionSettings("people").WriteVisibility.ShouldBe(expected);
        manager.GetConnectionSettings("other").WriteVisibility.ShouldBe(shared ?? WriteVisibility.Default);
    }
}
