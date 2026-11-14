using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;
using MeilisearchClient = Meilisearch.MeilisearchClient;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Term facets against a real Meilisearch, which has no multi-term, filter or nested facets.</summary>
public sealed class MeilisearchFacetsTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<MeilisearchClient> clientFactory)
    : FacetsTestBase(searchService, indexingProvider)
{
    protected override string UnsupportedComplexFacets => MeilisearchFacets.UnsupportedComplexFacets;

    /// <summary>
    /// Meilisearch returns 100 of the 101 values, alphabetically, so <c>z</c>, the value with the most documents, is
    /// not among them; the facet fails rather than leave it out.
    /// </summary>
    [Fact]
    public async Task SearchAsync_TermFacetWithMaxValuesPerFacetValues_ReturnsFailedResult()
    {
        var index = $"facets-{Guid.NewGuid():N}";
        try
        {
            var documents = Enumerable.Range(0, 100)
                .Select(i => new SearchDocument { Id = $"v{i}", Index = index, Fields = new Dictionary<string, IList<object>> { { Color, [$"v{i:D3}"] } } })
                .Concat(Enumerable.Range(0, 3).Select(i => new SearchDocument { Id = $"z{i}", Index = index, Fields = new Dictionary<string, IList<object>> { { Color, ["z"] } } }))
                .ToList();
            var indexed = await IndexingProvider.IndexMultipleDocumentsAsync(documents, Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));

            var result = await SearchService.SearchAsync(
                new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }] }, Token);

            result.IsFailed.ShouldBeTrue();
            result.Errors.Single().Message.ShouldContain("Facet colors has 100 or more values, the index's maxValuesPerFacet");
        }
        finally
        {
            try
            {
                await IndexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    /// <summary>
    /// With the index's <c>maxValuesPerFacet</c> lowered to 10, Meilisearch returns 10 of the 11 values,
    /// alphabetically, so <c>z</c>, the value with the most documents, is not among them; the facet fails rather than
    /// count against the default of 100 and leave it out.
    /// </summary>
    [Fact]
    public async Task SearchAsync_TermFacetWithIndexMaxValuesPerFacetValues_ReturnsFailedResult()
    {
        var index = $"facets-{Guid.NewGuid():N}";
        try
        {
            var documents = Enumerable.Range(0, 10)
                .Select(i => new SearchDocument { Id = $"v{i}", Index = index, Fields = new Dictionary<string, IList<object>> { { Color, [$"v{i:D3}"] } } })
                .Concat(Enumerable.Range(0, 3).Select(i => new SearchDocument { Id = $"z{i}", Index = index, Fields = new Dictionary<string, IList<object>> { { Color, ["z"] } } }))
                .ToList();
            var indexed = await IndexingProvider.IndexMultipleDocumentsAsync(documents, Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
            var client = await clientFactory.GetOrCreateClientAsync(index);
            var updated = await client.WaitForTaskAsync(
                (await client.Index(index).UpdateFacetingAsync(new global::Meilisearch.Faceting { MaxValuesPerFacet = 10 }, Token)).TaskUid,
                cancellationToken: Token);
            updated.Status.ShouldBe(global::Meilisearch.TaskInfoStatus.Succeeded);

            var result = await SearchService.SearchAsync(
                new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "colors", TermFieldName = Color }] }, Token);

            result.IsFailed.ShouldBeTrue();
            result.Errors.Single().Message.ShouldContain("Facet colors has 10 or more values, the index's maxValuesPerFacet");
        }
        finally
        {
            try
            {
                await IndexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }
}
