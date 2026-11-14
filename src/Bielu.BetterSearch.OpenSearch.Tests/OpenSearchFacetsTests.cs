using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>Facets against a real OpenSearch, whose facet fields are mapped as text in <see cref="Startup"/>.</summary>
public sealed class OpenSearchFacetsTests(
    ISearchServiceAsync searchService,
    IIndexingProviderAsync indexingProvider,
    IClientFactoryAsync<IOpenSearchClient> clientFactory) : FacetsTestBase(searchService, indexingProvider)
{
    protected override async Task RefreshAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        (await client.Indices.RefreshAsync(index, ct: Token)).IsValid.ShouldBeTrue();
    }

    private async Task<string> IndexTagsAsync()
    {
        var index = $"facets-{Guid.NewGuid():N}";
        var indexed = await IndexingProvider.IndexMultipleDocumentsAsync(
        [
            new SearchDocument { Id = "1", Index = index, Fields = new Dictionary<string, IList<object>> { { "Tag", ["New Arrival"] } } },
            new SearchDocument { Id = "2", Index = index, Fields = new Dictionary<string, IList<object>> { { "Tag", ["New Arrival"] } } },
            new SearchDocument { Id = "3", Index = index, Fields = new Dictionary<string, IList<object>> { { "Tag", ["Sale"] } } }
        ], Token);
        indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
        await RefreshAsync(index);
        return index;
    }

    /// <summary>Dynamic mapping makes <c>Tag</c> text with a keyword sub-field; the facet fails with a message saying what to do.</summary>
    [Fact]
    public async Task SearchAsync_TermFacetOnUnmappedTextField_ReturnsFailureNamingTheFix()
    {
        var index = await IndexTagsAsync();
        try
        {
            var result = await SearchService.SearchAsync(
                new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "tags", TermFieldName = "Tag" }] }, Token);

            result.IsFailed.ShouldBeTrue();
            var message = result.Errors.Single().Message;
            message.ShouldStartWith("A facet counts a field OpenSearch stores as text only");
            message.ShouldContain("fields.Tag");
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    [Fact]
    public async Task SearchAsync_TermFacetOnDynamicKeywordSubField_ReturnsWholeValues()
    {
        var index = await IndexTagsAsync();
        try
        {
            var result = await SearchService.SearchAsync(
                new SearchQuery { Index = index, FacetQueries = [new TermAggregation { AggregationName = "tags", TermFieldName = "Tag.keyword" }] }, Token);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.RetrieveBucketAggregation("tags")!.Buckets.Cast<KeyBucket>().Select(bucket => (bucket.Key, bucket.TotalOfDocuments))
                .ShouldBe([("New Arrival", 2L), ("Sale", 1L)]);
        }
        finally
        {
            await DeleteAsync(index);
        }
    }

    private async Task DeleteAsync(string index)
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
