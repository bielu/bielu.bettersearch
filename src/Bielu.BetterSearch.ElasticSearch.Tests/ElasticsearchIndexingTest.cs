using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

public sealed class ElasticsearchIndexingTest(
    IIndexingServiceAsync serviceAsync,
    IIndexingProviderAsync indexingProviderAsync,
    IClientFactoryAsync<ElasticsearchClient> clientFactory)
    : IndexingTestBase(serviceAsync, indexingProviderAsync)
{
    // Document 0 maps Count as a number, so a text value can't be stored.
    protected override SearchDocument CreateDocumentRejectedByEngine(string index) => new()
    {
        Id = "2",
        Type = "TestDocument",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { ["Count"] = ["not a number"] },
    };

    protected override async Task<long> CountDocumentsAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var token = TestContext.Current.CancellationToken;
        (await client.Indices.RefreshAsync(index, token)).IsValidResponse.ShouldBeTrue();
        var count = await client.CountAsync(new CountRequest(index), token);
        count.IsValidResponse.ShouldBeTrue();
        return count.Count;
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_WritesNotRefreshedYet_RemovesThemToo()
    {
        var index = $"remove-all-{Guid.NewGuid():N}";
        var token = TestContext.Current.CancellationToken;
        var client = await clientFactory.GetOrCreateClientAsync(index);
        try
        {
            // With no periodic refresh, the writes below stay unsearchable until something refreshes the index.
            var created = await client.Indices.CreateAsync(new CreateIndexRequest(index)
            {
                Settings = new IndexSettings { RefreshInterval = Duration.MinusOne }
            }, token);
            created.IsValidResponse.ShouldBeTrue(created.DebugInformation);
            var documents = new[] { "1", "2", "3" }.Select(id => new SearchDocument { Id = id, Type = "TestDocument", Index = index }).ToList();
            (await indexingProviderAsync.IndexMultipleDocumentsAsync(documents, token)).IsSuccess.ShouldBeTrue();

            var result = await indexingProviderAsync.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, token);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBe(3);
            (await CountDocumentsAsync(index)).ShouldBe(0);
        }
        finally
        {
            try
            {
                await client.Indices.DeleteAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }
}
