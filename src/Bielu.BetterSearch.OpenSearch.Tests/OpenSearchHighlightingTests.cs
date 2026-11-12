using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Highlighting against a real OpenSearch.</summary>
public sealed class OpenSearchHighlightingTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<IOpenSearchClient> clientFactory) : HighlightingTestBase(searchService, indexingProvider)
{
    protected override string? UnsupportedMessage => null;

    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, ct: Token)).IsValid.ShouldBeTrue();
    }
}
