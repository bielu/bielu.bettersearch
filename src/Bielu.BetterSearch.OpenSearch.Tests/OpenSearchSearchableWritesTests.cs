using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Writes against a real OpenSearch, with the shared settings set to wait until writes are searchable.</summary>
public sealed class OpenSearchSearchableWritesTests(OpenSearchResource openSearch) : SearchableWritesTestBase(CreateServices(openSearch))
{
    private static ServiceProvider CreateServices(OpenSearchResource openSearch)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddOpenSearch());
        services.AddOptions<OpenSearchOptions>().Configure(options =>
            options.SharedIndexSettings = new OpenSearchIndexSettings
            {
                ConnectionString = openSearch.EnsureStarted().ToString(),
                AuthenticationType = OpenSearchAuthenticationType.NONE,
                WriteVisibility = WriteVisibility.WaitUntilSearchable,
            });
        return services.BuildServiceProvider();
    }

    protected override void Dispose(bool disposing) => (Services as IDisposable)?.Dispose();
}
