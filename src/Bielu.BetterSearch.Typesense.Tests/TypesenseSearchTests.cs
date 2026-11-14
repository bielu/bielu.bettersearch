using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

/// <summary>Runs queries against a real Typesense and checks the engine behaviors the provider relies on.</summary>
public sealed class TypesenseSearchTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SearchDocument Document(string index, string id, Dictionary<string, IList<object>> fields) =>
        new() { Id = id, Type = "Item", Index = index, Fields = fields };

    /// <summary>Indexes <paramref name="documents"/> into an index of their own, runs <paramref name="test"/> and deletes the index.</summary>
    private async Task WithDocumentsAsync(Func<string, SearchDocument[]> documents, Func<string, Task> test)
    {
        var index = $"typesense-{Guid.NewGuid():N}";
        try
        {
            var created = documents(index);
            var indexed = await indexingProvider.IndexMultipleDocumentsAsync(created, Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
            indexed.Value.ShouldBe(created.Length);
            await test(index);
        }
        finally
        {
            try
            {
                await indexingProvider.DeleteIndexAsync(index, CancellationToken.None);
            }
            catch (Exception)
            {
                // Cleanup must not hide the test's own failure.
            }
        }
    }

    private Task WithPeopleAsync(Func<string, Task> test) => WithDocumentsAsync(index =>
    [
        Document(index, "1", new() { ["Name"] = ["Jane Doe"], ["Age"] = [25], ["Location"] = ["52.2297,21.0122"] }),
        Document(index, "2", new() { ["Name"] = ["John Doe"], ["Age"] = [30], ["Location"] = ["50.0647,19.9450"] }),
        Document(index, "3", new() { ["Name"] = ["Anna Nowak"] })
    ], test);

    [Fact]
    public Task SearchAsync_TermOnFieldNoDocumentHas_ReturnsNoHits() => WithPeopleAsync(async index =>
    {
        var ids = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Nickname", Value = "jd" }), Token);

        ids.ShouldBeEmpty();
    });

    [Fact]
    public Task SearchAsync_MustNotTermOnFieldNoDocumentHas_ReturnsEveryDocument() => WithPeopleAsync(async index =>
    {
        var ids = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUSTNOT, new TermSubQuery { Field = "Nickname", Value = "jd" }), Token);

        ids.ShouldBe(["1", "2", "3"]);
    });

    [Fact]
    public Task SearchAsync_MustNotTerm_ReturnsDocumentsWithoutTheFieldToo() => WithPeopleAsync(async index =>
    {
        var ids = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUSTNOT, new TermSubQuery { Field = "Age", Value = 25 }), Token);

        ids.ShouldBe(["2", "3"]);
    });

    [Fact]
    public Task SearchAsync_MustNotRange_ReturnsFailedResult() => WithPeopleAsync(async index =>
    {
        var result = await searchService.SearchAsync(
            SearchTestHelpers.Query(index, Occurance.MUSTNOT, new NumericRange { Field = "Age", MinValue = 20, MaxValue = 26 }), Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Typesense filters cannot negate a range");
    });

    [Fact]
    public Task SearchAsync_PageSizeAbove250_ReturnsFailedResult() => WithPeopleAsync(async index =>
    {
        var result = await searchService.SearchAsync(new SearchQuery { Index = index, PageSize = 251 }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("PageSize 251 is above 250");
    });

    [Fact]
    public Task SearchAsync_SpatialQuery_ReturnsOnlyDocumentsInTheRadius() => WithPeopleAsync(async index =>
    {
        var ids = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST,
            new SpatialSearchQuery { Field = "Location", Lat = 52.23, Lng = 21.01, Distance = 10 }), Token);

        ids.ShouldBe(["1"]);
    });

    [Fact]
    public Task SearchAsync_PrefixPhrase_MatchesWordsInOrderWithTheLastAsPrefix() => WithPeopleAsync(async index =>
    {
        var ids = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST,
            new PrefixPhraseSubQuery { Field = "Name", Value = "jane d" }), Token);

        ids.ShouldBe(["1"]);
    });

    [Fact]
    public Task SearchAsync_SortByString_OrdersHits() => WithPeopleAsync(async index =>
    {
        var result = await searchService.SearchAsync(new SearchQuery
        {
            Index = index,
            SortOrder = [new SortOrderField { FieldName = "Name", Desc = true }]
        }, Token);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["2", "1", "3"]);
    });

    [Fact]
    public Task SearchAsync_LongsAbove2To53_AreComparedExactly() => WithDocumentsAsync(index =>
    [
        Document(index, "low", new() { ["Big"] = [9007199254740992L] }),
        Document(index, "high", new() { ["Big"] = [9007199254740993L] })
    ], async index =>
    {
        var term = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Big", Value = 9007199254740993L }), Token);
        var range = await searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, new LongRange { Field = "Big", MinValue = 9007199254740993L }), Token);

        term.ShouldBe(["high"]);
        range.ShouldBe(["high"]);
    });

    [Fact]
    public Task SearchAsync_StoredValues_RoundTripWithTheirTypes() => WithDocumentsAsync(index =>
    [
        Document(index, "1", new() { ["Big"] = [9007199254740993L], ["Whole"] = [3.0], ["Flag"] = [true], ["Born"] = [new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc)] })
    ], async index =>
    {
        var result = await searchService.SearchAsync(new SearchQuery { Index = index }, Token);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        var fields = result.Value.Items.ShouldHaveSingleItem().Document.CustomProperties;
        fields["Big"].ShouldBe([9007199254740993L]);
        fields["Whole"].ShouldBe([3.0]);
        fields["Flag"].ShouldBe([true]);
        fields["Born"].ShouldBe(["2026-11-08T13:05:00Z"]);
    });

    [Fact]
    public Task RemoveDocumentsAsync_MoreIdsThanOneRequestNames_RemovesAndCountsEveryOne() => WithDocumentsAsync(
        index => Enumerable.Range(0, 150).Select(i => Document(index, $"doc-{i}", new() { ["N"] = [i] })).ToArray(),
        async index =>
        {
            var removed = await indexingProvider.RemoveDocumentsAsync(index, Enumerable.Range(0, 140).Select(i => $"doc-{i}"), Token);
            var left = await searchService.SearchAsync(new SearchQuery { Index = index }, Token);

            removed.IsSuccess.ShouldBeTrue(string.Join("; ", removed.Errors.Select(error => error.Message)));
            removed.Value.ShouldBe(140);
            left.Value.Pagination.TotalCount.ShouldBe(10);
        });

    [Fact]
    public Task SearchAsync_SortByDate_OrdersHits() => WithDocumentsAsync(index =>
    [
        Document(index, "1", new() { ["Born"] = [new DateTime(2000, 1, 1, 0, 0, 0, 1, DateTimeKind.Utc)] }),
        Document(index, "2", new() { ["Born"] = [new DateTime(2000, 1, 1, 0, 0, 0, 2, DateTimeKind.Utc)] }),
        Document(index, "3", new() { ["Born"] = [new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)] })
    ], async index =>
    {
        var result = await searchService.SearchAsync(new SearchQuery
        {
            Index = index,
            SortOrder = [new SortOrderField { FieldName = "Born", Desc = true }]
        }, Token);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["2", "1", "3"]);
    });

    [Fact]
    public Task SearchAsync_SortByMultiValuedField_ReturnsFailedResult() => WithDocumentsAsync(index =>
    [
        Document(index, "1", new() { ["Tags"] = ["a", "b"] })
    ], async index =>
    {
        var result = await searchService.SearchAsync(new SearchQuery
        {
            Index = index,
            SortOrder = [new SortOrderField { FieldName = "Tags" }]
        }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("Typesense sorts only on a field holding one number, date or string");
    });
}
