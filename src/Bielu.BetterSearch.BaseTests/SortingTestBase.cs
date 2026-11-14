using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// <c>SortOrder</c> through <see cref="ISearchServiceAsync"/>, inherited by every provider's tests. A malformed
/// entry fails the same way everywhere; a provider that sorts returns hits in the order the keys give.
/// </summary>
public abstract class SortingTestBase(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
{
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Whether the provider orders hits by <c>SortOrder</c>; one that doesn't keeps its own order.</summary>
    protected abstract bool SortsHits { get; }

    /// <summary>Makes the documents indexed so far searchable; nothing to do where indexing already is.</summary>
    protected virtual Task RefreshAsync(string index) => Task.CompletedTask;

    private static SearchDocument Item(string index, string id, int rank, long views) => new()
    {
        Id = id,
        Type = "Item",
        Index = index,
        Fields = new Dictionary<string, IList<object>>
        {
            { "Rank", [rank] },
            { "Views", [views] }
        }
    };

    private async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query)
    {
        query.PageSize = 100;
        return await searchService.SearchAsync(query, Token);
    }

    [Fact]
    public async Task SearchAsync_NullSortOrderEntry_ReturnsFailedResult()
    {
        var query = new SearchQuery
        {
            Index = $"sort-{Guid.NewGuid():N}",
            SortOrder = [new SortOrderField { FieldName = "Rank" }, null!]
        };

        var result = await SearchAsync(query);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("SortOrder holds a null entry.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_SortOrderEntryWithoutFieldName_ReturnsFailedResult(string? fieldName)
    {
        var query = new SearchQuery
        {
            Index = $"sort-{Guid.NewGuid():N}",
            SortOrder = [new SortOrderField { FieldName = "Rank" }, new SortOrderField { FieldName = fieldName!, Desc = true }]
        };

        var result = await SearchAsync(query);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("A SortOrder entry needs a FieldName.");
    }

    /// <summary>
    /// Sorts by <c>Rank</c> ascending, then by <c>Views</c> descending within a rank. The keys are listed in reverse
    /// of their <c>Order</c>, and every other reading of them (list order, <c>Views</c> first, either direction
    /// dropped) gives a different order.
    /// </summary>
    [Fact]
    public async Task SearchAsync_TwoKeySortOrder_ReturnsHitsInKeyOrder()
    {
        var index = $"sort-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingProvider.IndexMultipleDocumentsAsync(
            [
                Item(index, "a", 2, 1),
                Item(index, "b", 1, 5),
                Item(index, "c", 2, 9),
                Item(index, "d", 1, 2),
                Item(index, "e", 3, 3)
            ], Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
            await RefreshAsync(index);

            var unsorted = await SearchAsync(new SearchQuery { Index = index });
            var sorted = await SearchAsync(new SearchQuery
            {
                Index = index,
                SortOrder =
                [
                    new SortOrderField { FieldName = "Views", Desc = true, Order = 2 },
                    new SortOrderField { FieldName = "Rank", Order = 1 }
                ]
            });

            unsorted.IsSuccess.ShouldBeTrue(string.Join("; ", unsorted.Errors.Select(error => error.Message)));
            sorted.IsSuccess.ShouldBeTrue(string.Join("; ", sorted.Errors.Select(error => error.Message)));
            var ids = sorted.Value.Items.Select(item => item.Document.Id).ToList();
            ids.ShouldBe(SortsHits
                ? ["b", "d", "c", "a", "e"]
                : unsorted.Value.Items.Select(item => item.Document.Id).ToList());
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
}
