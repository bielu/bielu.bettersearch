using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Getting, counting and updating documents against a real OpenSearch.</summary>
public sealed class OpenSearchDocumentOperationsTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<IOpenSearchClient> clientFactory) : DocumentOperationsTestBase(searchService, indexingService, indexingProvider)
{
    protected override bool StoresFields => true;

    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, ct: Token)).IsValid.ShouldBeTrue();
    }
}
