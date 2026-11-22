using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

public sealed class OpenSearchClauseOccurrenceTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<IOpenSearchClient> clientFactory) : ClauseOccurrenceTestBase(searchService, indexingProvider)
{
    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, ct: Token)).IsValid.ShouldBeTrue();
    }
}
