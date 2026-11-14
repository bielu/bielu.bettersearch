using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using OpenSearch.Client;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>
/// Provider paths the shared <c>IndexingTestBase</c> does not reach: Core indexes many documents one by one,
/// so bulk indexing and removing every document are exercised here directly.
/// </summary>
public sealed class OpenSearchIndexingProviderTests(IIndexingProviderAsync provider, IClientFactoryAsync<IOpenSearchClient> clientFactory)
{
    private static string UniqueIndex() => $"opensearch-provider-{Guid.NewGuid():N}";

    private static List<SearchDocument> CreateDocuments(string index, int count) =>
        Enumerable.Range(1, count)
            .Select(i => new SearchDocument { Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Type = "TestDocument", Index = index })
            .ToList();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<long> CountAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        await client.Indices.RefreshAsync(index, ct: Token);
        return (await client.CountAsync<SearchDocument>(c => c.Index(index), Token)).Count;
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_DocumentsInOneIndex_IndexesEveryDocument()
    {
        var index = UniqueIndex();

        var result = await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 3), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        (await CountAsync(index)).ShouldBe(3);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_SameIdTwice_KeepsOneDocument()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        (await CountAsync(index)).ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_IndexWithDocuments_ReturnsDeletedCount()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();
        await CountAsync(index);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        (await CountAsync(index)).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_WritesNotRefreshedYet_RemovesThemToo()
    {
        var index = UniqueIndex();
        var client = await clientFactory.GetOrCreateClientAsync(index);
        try
        {
            // With no periodic refresh, the writes below stay unsearchable until something refreshes the index.
            var created = await client.Indices.CreateAsync(index, c => c.Settings(s => s.RefreshInterval(-1)), Token);
            created.IsValid.ShouldBeTrue(created.DebugInformation);
            (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 3), Token)).IsSuccess.ShouldBeTrue();

            var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBe(3);
            (await CountAsync(index)).ShouldBe(0);
        }
        finally
        {
            try
            {
                await client.Indices.DeleteAsync(index, ct: CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_MissingIndex_ReturnsFailure()
    {
        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = UniqueIndex() }, Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexExistsAsync_MissingIndex_ReturnsFalse()
    {
        var result = await provider.IndexExistsAsync(UniqueIndex(), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_MissingIndex_CreatesIndex()
    {
        var index = UniqueIndex();

        var result = await provider.EnsureIndexExistsAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        (await provider.IndexExistsAsync(index, Token)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateIndexAsync_IndexAlreadyExists_ReturnsFalse()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.CreateIndexAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteIndexAsync_MissingIndex_ReturnsFailure()
    {
        var result = await provider.DeleteIndexAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }
}
