using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<IOpenSearchClient> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    protected override async Task DocumentsChangedAsync(string index) =>
        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index);
}
