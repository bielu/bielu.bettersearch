using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticsearchClientFactoryTests
{
    private static ElasticsearchClientFactory CreateFactory(ElasticSearchOptions options)
    {
        var monitor = new FixedOptionsMonitor(options);
        return new ElasticsearchClientFactory(new ElasticSearchClientSettingsManager(monitor), monitor);
    }

    private static Uri NodeUri(ElasticsearchClient client) =>
        client.ElasticsearchClientSettings.NodePool.Nodes.Single().Uri;

    [Fact]
    public async Task GetOrCreateClientAsync_IndexWithoutConnectionString_UsesSharedClientAndLeavesOptionsUnchanged()
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
        options.Indexes!["people"].ConnectionString.ShouldBeNull();
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
}
