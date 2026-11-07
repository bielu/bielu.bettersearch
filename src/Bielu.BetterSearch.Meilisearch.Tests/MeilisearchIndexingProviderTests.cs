using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using MeilisearchClient = Meilisearch.MeilisearchClient;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>
/// Provider paths the shared <c>IndexingTestBase</c> does not reach: Core indexes many documents one by one,
/// so bulk indexing and removing every document are exercised here directly, along with the error paths.
/// </summary>
public sealed class MeilisearchIndexingProviderTests(
    IIndexingProviderAsync provider,
    IClientFactoryAsync<MeilisearchClient> clientFactory,
    IOptionsMonitor<MeilisearchOptions> options)
{
    private static string UniqueIndex() => $"meilisearch-provider-{Guid.NewGuid():N}";

    private static List<SearchDocument> CreateDocuments(string index, int count) =>
        Enumerable.Range(1, count)
            .Select(i => new SearchDocument
            {
                Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Type = "TestDocument",
                Index = index,
                Fields = new Dictionary<string, IList<object>> { ["Name"] = [$"Name {i}"] },
            })
            .ToList();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<long> CountAsync(string index)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index);
        return (await client.Index(index).GetStatsAsync(Token)).NumberOfDocuments;
    }

    [Fact]
    public async Task IndexDocumentAsync_NewDocument_StoresItUnderItsId()
    {
        var index = UniqueIndex();
        var document = CreateDocuments(index, 1)[0];

        var result = await provider.IndexDocumentAsync(document, Token);

        result.IsSuccess.ShouldBeTrue();
        var client = await clientFactory.GetOrCreateClientAsync(index);
        var stored = await client.Index(index).GetDocumentAsync<SearchDocument>(document.Id, cancellationToken: Token);
        stored.Id.ShouldBe(document.Id);
        stored.Type.ShouldBe(document.Type);
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
    public async Task IndexMultipleDocumentsAsync_DocumentsInTwoIndexes_IndexesEachIndex()
    {
        var first = UniqueIndex();
        var second = UniqueIndex();

        var result = await provider.IndexMultipleDocumentsAsync([.. CreateDocuments(first, 2), .. CreateDocuments(second, 1)], Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        (await CountAsync(first)).ShouldBe(2);
        (await CountAsync(second)).ShouldBe(1);
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
    public async Task IndexDocumentAsync_InvalidId_ReturnsFailureWithMeilisearchCode()
    {
        var document = new SearchDocument { Id = "not a valid id!", Type = "TestDocument", Index = UniqueIndex() };

        var result = await provider.IndexDocumentAsync(document, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Metadata["code"].ShouldBe("invalid_document_id");
    }

    [Fact]
    public async Task RemoveDocumentAsync_ExistingDocument_RemovesIt()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        var result = await provider.RemoveDocumentAsync("1", index, Token);

        result.IsSuccess.ShouldBeTrue();
        (await CountAsync(index)).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_IndexWithDocuments_ReturnsDeletedCount()
    {
        var index = UniqueIndex();
        (await provider.IndexMultipleDocumentsAsync(CreateDocuments(index, 2), Token)).IsSuccess.ShouldBeTrue();

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        (await CountAsync(index)).ShouldBe(0);
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
    public async Task IndexExistsAsync_ExistingIndex_ReturnsTrue()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.IndexExistsAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
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
    public async Task EnsureIndexExistsAsync_ExistingIndex_ReturnsTrue()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.EnsureIndexExistsAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ConcurrentCalls_AllReturnTrue()
    {
        var index = UniqueIndex();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => provider.EnsureIndexExistsAsync(index, Token)));

        results.ShouldAllBe(r => r.IsSuccess && r.Value);
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
    public async Task CreateIndexAsync_InvalidName_ReturnsFailure()
    {
        var result = await provider.CreateIndexAsync("not a valid index!", Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteIndexAsync_ExistingIndex_RemovesIt()
    {
        var index = UniqueIndex();
        (await provider.CreateIndexAsync(index, Token)).Value.ShouldBeTrue();

        var result = await provider.DeleteIndexAsync(index, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        (await provider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteIndexAsync_MissingIndex_ReturnsFailure()
    {
        var result = await provider.DeleteIndexAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexExistsAsync_WrongApiKey_ReturnsFailure()
    {
        var wrongKey = CreateProvider(options.CurrentValue.SharedIndexSettings.ConnectionString, "wrong-key");

        var result = await wrongKey.IndexExistsAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateIndexAsync_UnreachableServer_ReturnsFailure()
    {
        var unreachable = CreateProvider("http://127.0.0.1:1", null);

        var result = await unreachable.CreateIndexAsync(UniqueIndex(), Token);

        result.IsFailed.ShouldBeTrue();
    }

    private static MeilisearchIndexingProviderAsync CreateProvider(string connectionString, string? apiKey)
    {
        var monitor = new StaticOptionsMonitor(new MeilisearchOptions
        {
            SharedIndexSettings = new MeilisearchIndexSettings { ConnectionString = connectionString, ApiKey = apiKey },
        });
        return new MeilisearchIndexingProviderAsync(
            new MeilisearchClientFactory(new MeilisearchClientSettingsManager(monitor)),
            monitor,
            NullLogger<MeilisearchIndexingProviderAsync>.Instance);
    }
}
