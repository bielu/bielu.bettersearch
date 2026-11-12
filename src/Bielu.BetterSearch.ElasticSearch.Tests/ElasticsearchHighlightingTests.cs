using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>Highlighting against a real Elasticsearch.</summary>
public sealed class ElasticsearchHighlightingTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<ElasticsearchClient> clientFactory) : HighlightingTestBase(searchService, indexingProvider)
{
    protected override string? UnsupportedMessage => null;

    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, Token)).IsValidResponse.ShouldBeTrue();
    }
}
