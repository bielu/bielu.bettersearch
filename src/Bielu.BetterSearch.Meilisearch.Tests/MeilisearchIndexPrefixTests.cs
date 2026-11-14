using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;
using Bielu.BetterSearch.Tests;
using Meilisearch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>The index prefix against a real Meilisearch, which has no rebuilds.</summary>
public sealed class MeilisearchIndexPrefixTests(MeilisearchResource meilisearch) : IndexPrefixTestBase(CreateServices(meilisearch))
{
    private const string IndexPrefix = "prefix-test-";

    protected override string Prefix => IndexPrefix;

    private static ServiceProvider CreateServices(MeilisearchResource meilisearch)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddBetterSearch(x => x.AddMeilisearch());
        services.AddOptions<MeilisearchOptions>().Configure(options =>
        {
            options.IndexPrefix = IndexPrefix;
            options.SharedIndexSettings = new MeilisearchIndexSettings
            {
                ConnectionString = meilisearch.Endpoint!.ToString(),
                ApiKey = meilisearch.MasterKey,
            };
        });
        return services.BuildServiceProvider();
    }

    protected override async Task<bool> EngineIndexExistsAsync(string engineName)
    {
        var client = await Services.GetRequiredService<IClientFactoryAsync<MeilisearchClient>>().GetOrCreateClientAsync("any");
        try
        {
            await client.GetIndexAsync(engineName, TestContext.Current.CancellationToken);
            return true;
        }
        catch (MeilisearchApiError e) when (e.Code == "index_not_found")
        {
            return false;
        }
    }

    [Fact]
    public async Task StartRebuild_Meilisearch_FailsAsNotSupported()
    {
        var lifecycle = ScopedServices.GetRequiredService<IIndexLifecycleServiceAsync>();

        var rebuild = await lifecycle.StartRebuildAsync("people", TestContext.Current.CancellationToken);

        rebuild.Errors.ShouldHaveSingleItem().Message.ShouldBe("MeilisearchIndexingProviderAsync does not support index rebuilds: it does not implement IIndexLifecycleProviderAsync.");
    }

    protected override void Dispose(bool disposing) => (Services as IDisposable)?.Dispose();
}
