using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using FluentResults;
using Lifti;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Checks the Lifti indexing provider's own behavior, beyond what <c>IndexingTestBase</c> covers.</summary>
public sealed class LiftiIndexingProviderTests(IIndexingProviderAsync indexingProvider, ISearchServiceAsync searchService, ILiftiIndexManager indexManager)
{
    /// <summary>An index manager that cannot create an index, so every write that needs one fails.</summary>
    private sealed class FailingIndexManager : ILiftiIndexManager
    {
        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) =>
            Task.FromResult(Result.Fail<IFullTextIndex<string>>("no index"));

        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) =>
            Task.FromResult(Result.Fail<IFullTextIndex<string>?>("no index"));

        public Task<Result<bool>> ExistsAsync(string name) => Task.FromResult(Result.Ok(false));

        public Task<Result<bool>> DeleteIndexAsync(string name) => Task.FromResult(Result.Ok(false));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LiftiIndexingProviderAsync FailingProvider()
    {
        var manager = new FailingIndexManager();
        return new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);
    }

    private static SearchDocument Document(string index, string id, string name) => new()
    {
        Id = id,
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "Name", [name] } }
    };

    private async Task<List<string>> SearchIdsAsync(string index, string term)
    {
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = term });
        var result = await searchService.SearchAsync(query, Token);
        result.IsSuccess.ShouldBeTrue();
        return result.Value.Items.Select(item => item.Document.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_ManyIndexes_IndexesEachIntoItsOwn()
    {
        var first = $"bulk-{Guid.NewGuid():N}";
        var second = $"bulk-{Guid.NewGuid():N}";

        var result = await indexingProvider.IndexMultipleDocumentsAsync(
            [Document(first, "1", "alpha"), Document(second, "2", "alpha"), Document(first, "3", "alpha")], Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(3);
        (await SearchIdsAsync(first, "alpha")).ShouldBe(["1", "3"]);
        (await SearchIdsAsync(second, "alpha")).ShouldBe(["2"]);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_NoDocuments_ReturnsZero()
    {
        var result = await indexingProvider.IndexMultipleDocumentsAsync([], Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
    }

    [Fact]
    public async Task IndexDocumentAsync_DocumentWithNoFields_IsIndexed()
    {
        var index = $"index-{Guid.NewGuid():N}";

        (await indexingProvider.IndexDocumentAsync(new SearchDocument { Id = "1", Index = index }, Token)).IsSuccess.ShouldBeTrue();

        (await indexManager.GetOrCreateIndexAsync(index)).Value.Count.ShouldBe(1);
        (await SearchIdsAsync(index, "anything")).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveDocumentAsync_MissingDocument_ReturnsSuccess()
    {
        var index = $"index-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(Document(index, "1", "alpha"), Token)).IsSuccess.ShouldBeTrue();

        (await indexingProvider.RemoveDocumentAsync("missing", index, Token)).IsSuccess.ShouldBeTrue();

        (await SearchIdsAsync(index, "alpha")).ShouldBe(["1"]);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_IndexWithDocuments_ReturnsTheCountAndEmptiesIt()
    {
        var index = $"index-{Guid.NewGuid():N}";
        (await indexingProvider.IndexMultipleDocumentsAsync([Document(index, "1", "alpha"), Document(index, "2", "alpha")], Token)).Value.ShouldBe(2);

        var result = await indexingProvider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = index }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeTrue();
        (await SearchIdsAsync(index, "alpha")).ShouldBeEmpty();
    }

    /// <summary>Known defect: creating an index that already exists replaces it, losing its documents.</summary>
    [Fact]
    public async Task CreateIndexAsync_ExistingIndex_ReplacesItAndLosesItsDocuments()
    {
        var index = $"index-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(Document(index, "1", "alpha"), Token)).IsSuccess.ShouldBeTrue();

        var created = await indexingProvider.CreateIndexAsync(index, Token);

        created.Value.ShouldBeTrue();
        (await SearchIdsAsync(index, "alpha")).ShouldBeEmpty();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ExistingIndex_KeepsItsDocuments()
    {
        var index = $"index-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(Document(index, "1", "alpha"), Token)).IsSuccess.ShouldBeTrue();

        (await indexingProvider.EnsureIndexExistsAsync(index, Token)).Value.ShouldBeTrue();

        (await SearchIdsAsync(index, "alpha")).ShouldBe(["1"]);
    }

    [Fact]
    public async Task DeleteIndexAsync_MissingIndex_ReturnsFalse()
    {
        var result = await indexingProvider.DeleteIndexAsync($"missing-{Guid.NewGuid():N}", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteIndexAsync_ExistingIndex_ReturnsTrueAndDropsItsDocuments()
    {
        var index = $"index-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(Document(index, "1", "alpha"), Token)).IsSuccess.ShouldBeTrue();

        (await indexingProvider.DeleteIndexAsync(index, Token)).Value.ShouldBeTrue();

        (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeFalse();
        (await SearchIdsAsync(index, "alpha")).ShouldBeEmpty();
    }

    [Fact]
    public async Task IndexDocumentAsync_IndexCannotBeCreated_ReturnsFailedResult()
    {
        var result = await FailingProvider().IndexDocumentAsync(Document("any", "1", "alpha"), Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("no index");
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_IndexCannotBeCreated_ReturnsFailedResult()
    {
        var result = await FailingProvider().IndexMultipleDocumentsAsync([Document("any", "1", "alpha")], Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("no index");
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_IndexCannotBeCreated_ReturnsFailedResult()
    {
        var result = await FailingProvider().RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "any" }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("no index");
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_IndexCannotBeCreated_ReturnsFailedResult()
    {
        var result = await FailingProvider().EnsureIndexExistsAsync("any", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("no index");
    }

    [Fact]
    public async Task CreateIndexAsync_IndexCannotBeCreated_ReturnsFailedResult()
    {
        var result = await FailingProvider().CreateIndexAsync("any", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("no index");
    }

    [Fact]
    public async Task SearchAsync_IndexCannotBeCreated_ReturnsFailedResult()
    {
        var manager = new FailingIndexManager();
        var provider = new LiftiSearchProviderAsync(
            new LiftiQueryTranslatorService([]),
            manager,
            new LiftiBaseSearchModelMapper());

        var result = await provider.SearchAsync(new SearchQuery { Index = "any" }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("no index");
    }
}
