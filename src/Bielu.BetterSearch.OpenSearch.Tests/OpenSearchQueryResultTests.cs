using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchQueryResultTests(
    ISearchServiceAsync serviceAsync,
    IIndexingServiceAsync indexingServiceAsync,
    IClientFactoryAsync<IOpenSearchClient> clientFactory)
    : QueryResultBaseTests(serviceAsync, indexingServiceAsync)
{
    protected override async Task DocumentsIndexedAsync(string index) =>
        await (await clientFactory.GetOrCreateClientAsync(index)).Indices.RefreshAsync(index);
}
