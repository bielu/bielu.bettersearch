using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>Sorting against a real Elasticsearch.</summary>
public sealed class ElasticsearchSortingTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<ElasticsearchClient> clientFactory) : SortingTestBase(searchService, indexingProvider)
{
    protected override bool SortsHits => true;

    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, Token)).IsValidResponse.ShouldBeTrue();
    }
}
