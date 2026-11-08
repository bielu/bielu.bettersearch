using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchClientSettingsManagerTests
{
    private static IElasticsearchClientSettings GetSettings(ElasticSearchOptions options, string index) =>
        new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(options)).GetOrCreateClientSettings(index);

    [Fact]
    public void GetOrCreateClientSettings_BoundIndexKeyCasingDiffers_UsesIndexSettings()
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
    public void GetOrCreateClientSettings_AssignedIndexKeyCasingDiffers_UsesIndexSettings()
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
    public void GetOrCreateClientSettings_NoIndexSettings_UsesSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }

    [Fact]
    public void GetOrCreateClientSettings_IndexWithoutConnectionString_LeavesOptionsUnchanged()
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
    public void GetOrCreateSharedClientSettings_IndexNamedDefault_UsesSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
            Indexes = new Dictionary<string, ElasticSearchIndexSettings>
            {
                ["default"] = new() { ConnectionString = "http://other:9200" },
            },
        };

        IElasticsearchClientSettings settings =
            new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(options)).GetOrCreateSharedClientSettings();

        settings.NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }
}
