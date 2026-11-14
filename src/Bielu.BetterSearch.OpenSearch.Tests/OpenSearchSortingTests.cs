using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Sorting against a real OpenSearch.</summary>
public sealed class OpenSearchSortingTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<IOpenSearchClient> clientFactory) : SortingTestBase(searchService, indexingProvider)
{
    protected override bool SortsHits => true;

    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, ct: Token)).IsValid.ShouldBeTrue();
    }
}
