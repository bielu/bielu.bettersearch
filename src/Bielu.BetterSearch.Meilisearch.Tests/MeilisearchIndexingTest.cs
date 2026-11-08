using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using MeilisearchClient = Meilisearch.MeilisearchClient;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public sealed class MeilisearchIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<MeilisearchClient> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    // The provider waits for each write task, so the stats already reflect it.
    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        return (await client.Index(index).GetStatsAsync(TestContext.Current.CancellationToken)).NumberOfDocuments;
    }
}
