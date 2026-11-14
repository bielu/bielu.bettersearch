using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>Requests against a real Elasticsearch through a sticky pool whose first node can't be reached.</summary>
public sealed class ElasticsearchNodeFailoverTests(ElasticsearchResource elasticsearch) : NodeFailoverTestBase
{
    protected override string EngineNode => elasticsearch.EnsureStarted().ToString();

    protected override ServiceProvider CreateServices(string connectionString, IList<string> additionalNodes)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddElasticSearch());
        services.AddOptions<ElasticSearchOptions>().Configure(options =>
            options.SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = connectionString,
                AdditionalNodes = additionalNodes,
                // Sticky tries the nodes in the order listed, so every request starts at the unreachable one.
                PoolType = NodePoolType.Sticky,
                AuthenticationType = AuthenticationType.NONE,
                WriteVisibility = WriteVisibility.WaitUntilSearchable,
            });
        return services.BuildServiceProvider();
    }
}
