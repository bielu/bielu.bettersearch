using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public class ElasticSearchClientSettingsManagerTests
{
    private static IElasticsearchClientSettings GetSettings(ElasticSearchOptions options, string index) =>
        new ElasticSearchClientSettingsManager(new StaticOptionsMonitor(options)).GetOrCreateClientSettings(index);

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
    public void GetOrCreateClientSettings_NoIndexSettings_UsesSharedConnectionString()
    {
        var options = new ElasticSearchOptions
        {
            SharedIndexSettings = new ElasticSearchIndexSettings { ConnectionString = "http://shared:9200" },
        };

        GetSettings(options, "people").NodePool.Nodes.Single().Uri.ShouldBe(new Uri("http://shared:9200"));
    }

    private sealed class StaticOptionsMonitor(ElasticSearchOptions options) : IOptionsMonitor<ElasticSearchOptions>
    {
        public ElasticSearchOptions CurrentValue => options;

        public ElasticSearchOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<ElasticSearchOptions, string?> listener) => null;
    }
}
