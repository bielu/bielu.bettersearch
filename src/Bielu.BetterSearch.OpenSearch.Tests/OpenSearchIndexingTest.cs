using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<IOpenSearchClient> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var token = TestContext.Current.CancellationToken;
        (await client.Indices.RefreshAsync(index, ct: token)).IsValid.ShouldBeTrue();
        var count = await client.CountAsync<SearchDocument>(c => c.Index(index), token);
        count.IsValid.ShouldBeTrue();
        return count.Count;
    }
}
