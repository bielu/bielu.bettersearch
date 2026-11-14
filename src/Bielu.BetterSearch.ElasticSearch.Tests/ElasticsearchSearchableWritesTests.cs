using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>Writes against a real Elasticsearch, with the shared settings set to wait until writes are searchable.</summary>
public sealed class ElasticsearchSearchableWritesTests(ElasticsearchResource elasticsearch) : SearchableWritesTestBase(CreateServices(elasticsearch))
{
    private static ServiceProvider CreateServices(ElasticsearchResource elasticsearch)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddElasticSearch());
        services.AddOptions<ElasticSearchOptions>().Configure(options =>
            options.SharedIndexSettings = new ElasticSearchIndexSettings
            {
                ConnectionString = elasticsearch.EnsureStarted().ToString(),
                AuthenticationType = AuthenticationType.NONE,
                WriteVisibility = WriteVisibility.WaitUntilSearchable,
            });
        return services.BuildServiceProvider();
    }

    protected override void Dispose(bool disposing) => (Services as IDisposable)?.Dispose();
}
