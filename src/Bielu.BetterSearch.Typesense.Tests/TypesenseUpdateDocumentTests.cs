using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>Gets and updates against a real Typesense that depend on how the provider stores and addresses documents.</summary>
public sealed class TypesenseUpdateDocumentTests(
    ISearchServiceAsync searchService,
    IIndexingServiceAsync indexingService,
    IIndexingProviderAsync indexingProvider)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// <c>export</c> and <c>search</c> name endpoints under a collection's documents. A get of a document with that id
    /// returns that document, and only it: <c>search</c> is not indexed, so it is <c>null</c> rather than another
    /// document.
    /// </summary>
    [Fact]
    public async Task GetDocumentAsync_IdNamingAnEndpoint_ReturnsOnlyThatDocument()
    {
        var index = $"get-endpoint-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingService.IndexMultipleDocumentsAsync([
                new SearchDocument { Id = "export", Type = "Item", Index = index, Fields = new Dictionary<string, IList<object>> { { "Rank", [3L] } } },
                new SearchDocument { Id = "other", Type = "Item", Index = index, Fields = new Dictionary<string, IList<object>> { { "Rank", [7L] } } }
            ], Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));

            var export = await searchService.GetDocumentAsync(index, "export", Token);
            var search = await searchService.GetDocumentAsync(index, "search", Token);

            export.IsSuccess.ShouldBeTrue(string.Join("; ", export.Errors.Select(error => error.Message)));
            export.Value.ShouldNotBeNull().Id.ShouldBe("export");
            export.Value.Fields["Rank"].ShouldBe([3L]);
            search.IsSuccess.ShouldBeTrue(string.Join("; ", search.Errors.Select(error => error.Message)));
            search.Value.ShouldBeNull();
        }
        finally
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
    }

    /// <summary>
    /// <c>Rank</c> changes from an integer to a string: the string matches, and the integer copy the document held
    /// before is removed, so a range on the old value no longer matches it.
    /// </summary>
    [Fact]
    public async Task UpdateDocumentAsync_ValueOfAnotherKind_RemovesTheOldTypedCopy()
    {
        var index = $"update-kind-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingService.IndexMultipleDocumentsAsync([
                new SearchDocument { Id = "a", Type = "Item", Index = index, Fields = new Dictionary<string, IList<object>> { { "Rank", [3L] } } },
                new SearchDocument { Id = "b", Type = "Item", Index = index, Fields = new Dictionary<string, IList<object>> { { "Rank", [7L] } } }
            ], Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));

            var result = await indexingService.UpdateDocumentAsync(index, "b",
                new Dictionary<string, IList<object>> { { "Rank", ["top"] } }, Token);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.ShouldBeTrue();
            var range = SearchTestHelpers.Query(index, Occurance.MUST, new LongRange { Field = "Rank", MinValue = 0, MaxValue = 10 });
            (await searchService.SearchIdsAsync(range, Token)).ShouldBe(["a"]);
            var term = SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Rank", Value = "top" });
            (await searchService.SearchIdsAsync(term, Token)).ShouldBe(["b"]);
            (await searchService.GetDocumentAsync(index, "b", Token)).Value.ShouldNotBeNull().Fields["Rank"].ShouldBe(["top"]);
        }
        finally
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
    }
}
