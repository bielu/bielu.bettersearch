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
    protected override bool BulkIndexingIsAllOrNothing => true;

    // Meilisearch accepts only letters, digits, - and _ in an id, and fails the whole task for one it rejects.
    protected override SearchDocument CreateDocumentRejectedByEngine(string index) =>
        new() { Id = "not valid!", Type = "TestDocument", Index = index };

    // The provider waits for each write task, so the stats already reflect it.
    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        return (await client.Index(index).GetStatsAsync(TestContext.Current.CancellationToken)).NumberOfDocuments;
    }
}
