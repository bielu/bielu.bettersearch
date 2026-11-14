using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// <c>GetDocumentAsync</c>, <c>CountAsync</c> and <c>UpdateDocumentAsync</c> through the services, inherited by every
/// provider's tests. A provider that keeps no stored fields fails a get and an update with its own message.
/// </summary>
public abstract class DocumentOperationsTestBase(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider)
{
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly DateTime Published = new(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc);

    /// <summary>Whether the provider stores fields, so it can return a document and merge an update into it.</summary>
    protected abstract bool StoresFields { get; }

    /// <summary>The message a provider that does not store fields fails a get with.</summary>
    protected virtual string UnsupportedGetMessage => string.Empty;

    /// <summary>The message a provider that does not store fields fails an update with.</summary>
    protected virtual string UnsupportedUpdateMessage => string.Empty;

    /// <summary>Whether counting on a missing index fails, as a search there does; Lifti creates the index instead.</summary>
    protected virtual bool CountOnMissingIndexFails => true;

    /// <summary>Makes the documents indexed so far searchable; nothing to do where indexing already is.</summary>
    protected virtual Task RefreshAsync(string index) => Task.CompletedTask;

    private static SearchDocument Item(string index, string id, string category, long rank) => new()
    {
        Id = id,
        Type = "Item",
        Index = index,
        Fields = new Dictionary<string, IList<object>>
        {
            { "Category", [category] },
            { "Rank", [rank] },
            { "Published", [Published] }
        }
    };

    private static SearchQuery CategoryQuery(string index, string category) =>
        SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Category", Value = category });

    private async Task IndexAsync(string index, params SearchDocument[] documents)
    {
        var indexed = await indexingService.IndexMultipleDocumentsAsync(documents, Token);
        indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
        await RefreshAsync(index);
    }

    private async Task DeleteIndexAsync(string index)
    {
        try
        {
            await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
        }
        catch
        {
            // Cleanup must not hide the test's own failure.
        }
    }

    [Fact]
    public async Task GetDocumentAsync_IndexedDocument_ReturnsItsFields()
    {
        var index = $"get-{Guid.NewGuid():N}";
        try
        {
            await IndexAsync(index, Item(index, "a", "news", 3), Item(index, "b", "blog", 7));

            var result = await searchService.GetDocumentAsync(index, "b", Token);

            if (!StoresFields)
            {
                result.IsFailed.ShouldBeTrue();
                result.Errors.Single().Message.ShouldBe(UnsupportedGetMessage);
                return;
            }

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            var document = result.Value.ShouldNotBeNull();
            document.Id.ShouldBe("b");
            document.Type.ShouldBe("Item");
            document.Index.ShouldBe(index);
            document.Fields["Category"].ShouldBe(["blog"]);
            document.Fields["Rank"].ShouldBe([7L]);
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task GetDocumentAsync_MissingDocument_ReturnsNull()
    {
        var index = $"get-{Guid.NewGuid():N}";
        try
        {
            await IndexAsync(index, Item(index, "a", "news", 3));

            var result = await searchService.GetDocumentAsync(index, "missing", Token);

            if (!StoresFields)
            {
                result.Errors.Single().Message.ShouldBe(UnsupportedGetMessage);
                return;
            }

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBeNull();
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task GetDocumentAsync_MissingIndex_ReturnsNull()
    {
        var result = await searchService.GetDocumentAsync($"get-missing-{Guid.NewGuid():N}", "a", Token);

        if (!StoresFields)
        {
            result.Errors.Single().Message.ShouldBe(UnsupportedGetMessage);
            return;
        }

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetDocumentAsync_NoId_ReturnsFailedResult(string? id)
    {
        var result = await searchService.GetDocumentAsync($"get-{Guid.NewGuid():N}", id!, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("GetDocumentAsync needs a document id.");
    }

    /// <summary>The page asked for holds one hit, so a count read from it, or capped at the page, would be 1.</summary>
    [Fact]
    public async Task CountAsync_Query_ReturnsEveryMatchIgnoringThePage()
    {
        var index = $"count-{Guid.NewGuid():N}";
        try
        {
            await IndexAsync(index,
                Item(index, "a", "news", 1),
                Item(index, "b", "blog", 2),
                Item(index, "c", "news", 3),
                Item(index, "d", "news", 4));
            var query = CategoryQuery(index, "news");
            query.PageSize = 1;
            query.Page = 2;

            var news = await searchService.CountAsync(query, Token);
            var none = await searchService.CountAsync(CategoryQuery(index, "sport"), Token);

            news.IsSuccess.ShouldBeTrue(string.Join("; ", news.Errors.Select(error => error.Message)));
            news.Value.ShouldBe(3);
            none.IsSuccess.ShouldBeTrue(string.Join("; ", none.Errors.Select(error => error.Message)));
            none.Value.ShouldBe(0);
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task CountAsync_MissingIndex_FailsAsSearchDoes()
    {
        var index = $"count-missing-{Guid.NewGuid():N}";
        try
        {
            var result = await searchService.CountAsync(CategoryQuery(index, "news"), Token);

            if (CountOnMissingIndexFails)
            {
                result.IsFailed.ShouldBeTrue();
                result.Errors.Single().Message.ShouldContain("index");
            }
            else
            {
                result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
                result.Value.ShouldBe(0);
            }
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task CountAsync_NoIndex_ReturnsFailedResult()
    {
        var result = await searchService.CountAsync(new SearchQuery { Index = " " }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("The query needs an Index.");
    }

    /// <summary>
    /// Changes <c>Category</c> only. The new value is stored and searchable, the old one no longer matches, and the
    /// fields left out, <c>Rank</c> and the date <c>Published</c>, keep their values and stay searchable.
    /// </summary>
    [Fact]
    public async Task UpdateDocumentAsync_ExistingDocument_ReplacesNamedFieldsAndKeepsOthers()
    {
        var index = $"update-{Guid.NewGuid():N}";
        try
        {
            await IndexAsync(index, Item(index, "a", "news", 3), Item(index, "b", "news", 7));

            var result = await indexingService.UpdateDocumentAsync(index, "b",
                new Dictionary<string, IList<object>> { { "Category", ["blog"] } }, Token);

            if (!StoresFields)
            {
                result.IsFailed.ShouldBeTrue();
                result.Errors.Single().Message.ShouldBe(UnsupportedUpdateMessage);
                return;
            }

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBeTrue();
            await RefreshAsync(index);

            var document = (await searchService.GetDocumentAsync(index, "b", Token)).Value.ShouldNotBeNull();
            document.Fields["Category"].ShouldBe(["blog"]);
            document.Fields["Rank"].ShouldBe([7L]);
            (await searchService.SearchIdsAsync(CategoryQuery(index, "blog"), Token)).ShouldBe(["b"]);
            (await searchService.SearchIdsAsync(CategoryQuery(index, "news"), Token)).ShouldBe(["a"]);
            var published = SearchTestHelpers.Query(index, Occurance.MUST,
                new DateRangeQuery { Field = "Published", MinValue = Published.AddDays(-1), MaxValue = Published.AddDays(1) });
            (await searchService.SearchIdsAsync(published, Token)).ShouldBe(["a", "b"]);
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task UpdateDocumentAsync_MissingDocument_ReturnsFalseAndCreatesNothing()
    {
        var index = $"update-{Guid.NewGuid():N}";
        try
        {
            await IndexAsync(index, Item(index, "a", "news", 3));

            var result = await indexingService.UpdateDocumentAsync(index, "missing",
                new Dictionary<string, IList<object>> { { "Category", ["blog"] } }, Token);

            if (!StoresFields)
            {
                result.Errors.Single().Message.ShouldBe(UnsupportedUpdateMessage);
                return;
            }

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBeFalse();
            await RefreshAsync(index);
            (await searchService.GetDocumentAsync(index, "missing", Token)).Value.ShouldBeNull();
            (await searchService.CountAsync(new SearchQuery { Index = index }, Token)).Value.ShouldBe(1);
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task UpdateDocumentAsync_MissingIndex_ReturnsFalseAndCreatesNoIndex()
    {
        var index = $"update-missing-{Guid.NewGuid():N}";
        try
        {
            var result = await indexingService.UpdateDocumentAsync(index, "a",
                new Dictionary<string, IList<object>> { { "Category", ["blog"] } }, Token);

            if (!StoresFields)
            {
                result.Errors.Single().Message.ShouldBe(UnsupportedUpdateMessage);
                return;
            }

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBeFalse();
            var exists = await indexingProvider.IndexExistsAsync(index, Token);
            exists.IsSuccess.ShouldBeTrue(string.Join("; ", exists.Errors.Select(error => error.Message)));
            exists.Value.ShouldBeFalse();
        }
        finally
        {
            await DeleteIndexAsync(index);
        }
    }

    [Fact]
    public async Task UpdateDocumentAsync_NullValue_ReturnsFailedResult()
    {
        var result = await indexingService.UpdateDocumentAsync($"update-{Guid.NewGuid():N}", "a",
            new Dictionary<string, IList<object>> { { "Category", ["blog", null!] } }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("The update of document a has null values in fields (Category); leave them out of the list.");
    }

    [Fact]
    public async Task UpdateDocumentAsync_NoFields_ReturnsFailedResult()
    {
        var result = await indexingService.UpdateDocumentAsync($"update-{Guid.NewGuid():N}", "a",
            new Dictionary<string, IList<object>>(), Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("The update of document a has no fields; pass at least one field to change.");
    }
}
