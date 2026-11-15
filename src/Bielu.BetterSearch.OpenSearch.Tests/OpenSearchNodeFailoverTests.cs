using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.DependencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DependencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Requests against a real OpenSearch through a sticky pool whose first node can't be reached.</summary>
public sealed class OpenSearchNodeFailoverTests(OpenSearchResource openSearch) : NodeFailoverTestBase
{
    protected override string EngineNode => openSearch.EnsureStarted().ToString();

    protected override ServiceProvider CreateServices(string connectionString, IList<string> additionalNodes)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddOpenSearch());
        services.AddOptions<OpenSearchOptions>().Configure(options =>
            options.SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = connectionString,
                AdditionalNodes = additionalNodes,
                // Sticky tries the nodes in the order listed, so every request starts at the unreachable one.
                PoolType = NodePoolType.Sticky,
                AuthenticationType = OpenSearchAuthenticationType.None,
                WriteVisibility = WriteVisibility.WaitUntilSearchable,
            });
        return services.BuildServiceProvider();
    }
}
