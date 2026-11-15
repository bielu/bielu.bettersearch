using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// A clause's occurrence through <see cref="ISearchServiceAsync"/>, inherited by every provider's tests. The occurrence
/// lives on the sub-query only, so a clause means the same at the top level and inside a bool, whether it was added
/// through <c>Add</c> or set in an initializer.
/// </summary>
public abstract class ClauseOccurrenceTestBase(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
{
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Makes the documents indexed so far searchable; nothing to do where indexing already is.</summary>
    protected virtual Task RefreshAsync(string index) => Task.CompletedTask;

    public enum Placement
    {
        TopLevelThroughAdd,
        TopLevelInInitializer,
        NestedThroughAdd,
        NestedInInitializer
    }

    private static SearchDocument Person(string index, string id, string city) => new()
    {
        Id = id,
        Type = "Person",
        Index = index,
        Fields = new Dictionary<string, IList<object>> { { "City", [city] } }
    };

    private static TermSubQuery City(string city, Occurrence occurrence = Occurrence.Must) =>
        new() { Field = "City", Value = city, Occurrence = occurrence };

    /// <summary>
    /// Two SHOULD clauses, built the way <paramref name="placement"/> names. The sub-queries of the <c>Add</c> forms
    /// start as MUST, the default, so a query that read that instead of the occurrence they were added with would
    /// need both cities at once and match nothing.
    /// </summary>
    private static SearchQuery TwoShouldClauses(string index, Placement placement)
    {
        var query = new SearchQuery { Index = index, PageSize = 100 };
        switch (placement)
        {
            case Placement.TopLevelThroughAdd:
                query.Add(Occurrence.Should, City("warsaw"));
                query.Add(Occurrence.Should, City("krakow"));
                break;
            case Placement.TopLevelInInitializer:
                query.Query = [City("warsaw", Occurrence.Should), City("krakow", Occurrence.Should)];
                break;
            case Placement.NestedThroughAdd:
                var nested = new BoolSearchSubQuery();
                nested.Add(Occurrence.Should, City("warsaw"));
                nested.Add(Occurrence.Should, City("krakow"));
                query.Add(Occurrence.Must, nested);
                break;
            case Placement.NestedInInitializer:
                query.Query = [new BoolSearchSubQuery { NestedQueries = [City("warsaw", Occurrence.Should), City("krakow", Occurrence.Should)] }];
                break;
        }

        return query;
    }

    private async Task WithPeopleAsync(Func<string, Task> test)
    {
        var index = $"occurrence-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingProvider.IndexMultipleDocumentsAsync(
            [
                Person(index, "a", "warsaw"),
                Person(index, "b", "krakow"),
                Person(index, "c", "gdansk")
            ], Token);
            indexed.IsSuccess.ShouldBeTrue(string.Join("; ", indexed.Errors.Select(error => error.Message)));
            await RefreshAsync(index);

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

    [Theory]
    [InlineData(Placement.TopLevelThroughAdd)]
    [InlineData(Placement.TopLevelInInitializer)]
    [InlineData(Placement.NestedThroughAdd)]
    [InlineData(Placement.NestedInInitializer)]
    public async Task SearchAsync_ShouldClausesAnywhere_MatchEitherClause(Placement placement) => await WithPeopleAsync(async index =>
    {
        var ids = await searchService.SearchIdsAsync(TwoShouldClauses(index, placement), Token);

        ids.ShouldBe(["a", "b"]);
    });

    /// <summary>Boosts are fractions on every engine, so one below 1 is sent as it is, and changes no hit.</summary>
    [Fact]
    public async Task SearchAsync_FractionalBoost_IsAccepted() => await WithPeopleAsync(async index =>
    {
        var query = new SearchQuery { Index = index, PageSize = 100 };
        var warsaw = City("warsaw");
        warsaw.BoostValue = 0.5;
        query.Add(Occurrence.Should, warsaw);
        query.Add(Occurrence.Should, new BoolSearchSubQuery { BoostValue = 1.5, NestedQueries = [City("krakow")] });

        var ids = await searchService.SearchIdsAsync(query, Token);

        ids.ShouldBe(["a", "b"]);
    });
}
