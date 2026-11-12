using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.FluentApi.Fluent;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

/// <summary>
/// Highlighting through <see cref="ISearchServiceAsync"/>, inherited by every provider's tests. A provider that
/// highlights returns the marked fragments on each hit; one that can't returns a failed result naming why.
/// </summary>
public abstract class HighlightingTestBase(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
{
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// <c>null</c> when the provider highlights; otherwise text that only its error for an unsupported highlight
    /// request contains.
    /// </summary>
    protected abstract string? UnsupportedMessage { get; }

    /// <summary>Makes the documents indexed so far searchable; nothing to do where indexing already is.</summary>
    protected virtual Task RefreshAsync(string index) => Task.CompletedTask;

    private static SearchDocument Person(string index, string id, string name, string city, params string[] tags) => new()
    {
        Id = id,
        Type = "Person",
        Index = index,
        Fields = new Dictionary<string, IList<object>>
        {
            { "Name", [name] },
            { "City", [city] },
            { "Tags", tags.Cast<object>().ToList() }
        }
    };

    /// <summary>Runs <paramref name="test"/> on four people in an index of their own, deleted afterwards.</summary>
    private async Task WithPeopleAsync(Func<string, Task> test)
    {
        var index = $"highlight-{Guid.NewGuid():N}";
        try
        {
            var indexed = await indexingProvider.IndexMultipleDocumentsAsync(
            [
                Person(index, "jane", "Jane Doe", "Krakow", "red apple", "green pear"),
                Person(index, "john", "John Smith", "Warsaw", "blue plum"),
                Person(index, "anna", "Anna Nowak", "Gdansk"),
                Person(index, "tom", "Tom <b>Cat</b> & Jerry", "Lodz")
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

    /// <summary>Doe in the name or Warsaw as the city: Jane and John.</summary>
    private static SearchQuery DoeOrWarsaw(string index)
    {
        var query = new SearchQuery { Index = index };
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "doe" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "City", Value = "warsaw" });
        return query;
    }

    /// <summary>
    /// The hits of <paramref name="query"/> by id, or <c>null</c> after checking that a provider that can't
    /// highlight failed with its message.
    /// </summary>
    private async Task<Dictionary<string, ISearchHit>?> HighlightAsync(SearchQuery query)
    {
        var result = await searchService.SearchAsync(query, Token);
        if (UnsupportedMessage is { } unsupported)
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Single().Message.ShouldContain(unsupported);
            return null;
        }

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value.Items.ToDictionary(hit => hit.Document.Id);
    }

    private async Task ShouldFailAsync(SearchQuery query, string message)
    {
        var result = await searchService.SearchAsync(query, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(UnsupportedMessage ?? message);
    }

    [Fact]
    public async Task SearchAsync_HighlightedField_MarksTheMatchedTermOnTheMatchingHitOnly()
    {
        await WithPeopleAsync(async index =>
        {
            var query = DoeOrWarsaw(index);
            query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name" });

            if (await HighlightAsync(query) is not { } hits)
            {
                return;
            }

            hits.Keys.Order(StringComparer.Ordinal).ShouldBe(["jane", "john"]);
            hits["jane"].Highlights.Keys.ShouldBe(["Name"]);
            hits["jane"].Highlights["Name"].ShouldBe(["Jane <em>Doe</em>"]);
            // John matched on City, which was not asked for, so he has no entry rather than an empty one.
            hits["john"].Highlights.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task SearchAsync_AllFields_MarksEachFieldTheQueryMatched()
    {
        await WithPeopleAsync(async index =>
        {
            var query = DoeOrWarsaw(index);
            query.HighlightQueries.Add(new BaseHighlightQuery { IsAllField = true });

            if (await HighlightAsync(query) is not { } hits)
            {
                return;
            }

            hits["jane"].Highlights.Keys.ShouldBe(["Name"]);
            hits["jane"].Highlights["Name"].ShouldBe(["Jane <em>Doe</em>"]);
            hits["john"].Highlights.Keys.ShouldBe(["City"]);
            hits["john"].Highlights["City"].ShouldBe(["<em>Warsaw</em>"]);
        });
    }

    [Fact]
    public async Task SearchAsync_OwnHighlightQuery_MarksItsTermsRatherThanTheMainQuerys()
    {
        await WithPeopleAsync(async index =>
        {
            var query = SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" });
            query.HighlightQueries.Add(new BaseHighlightQuery
            {
                Field = "Name",
                Queries = { [Occurance.MUST] = new TermSubQuery { Field = "Name", Value = "jane" } }
            });

            if (await HighlightAsync(query) is not { } hits)
            {
                return;
            }

            hits.Keys.ShouldBe(["jane"]);
            hits["jane"].Highlights["Name"].ShouldBe(["<em>Jane</em> Doe"]);
        });
    }

    [Fact]
    public async Task SearchAsync_MultiValuedField_ReturnsOnlyTheValuesThatMatched()
    {
        await WithPeopleAsync(async index =>
        {
            var query = SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Tags", Value = "pear" });
            query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Tags" });

            if (await HighlightAsync(query) is not { } hits)
            {
                return;
            }

            hits.Keys.ShouldBe(["jane"]);
            hits["jane"].Highlights["Tags"].ShouldBe(["green <em>pear</em>"]);
        });
    }

    [Fact]
    public async Task SearchAsync_MarkupInTheText_IsEscapedSoOnlyTheTagsAreMarkup()
    {
        await WithPeopleAsync(async index =>
        {
            var query = SearchTestHelpers.Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "cat" });
            query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name" });

            if (await HighlightAsync(query) is not { } hits)
            {
                return;
            }

            hits.Keys.ShouldBe(["tom"]);
            hits["tom"].Highlights["Name"].ShouldBe(["Tom &lt;b&gt;<em>Cat</em>&lt;&#x2F;b&gt; &amp; Jerry"]);
        });
    }

    [Fact]
    public async Task SearchAsync_HighlightWithoutField_ReturnsFailure()
    {
        var query = new SearchQuery { Index = $"highlight-{Guid.NewGuid():N}" };
        query.HighlightQueries.Add(new BaseHighlightQuery());

        await ShouldFailAsync(query, "needs a Field, or IsAllField");
    }

    [Fact]
    public async Task SearchAsync_SameFieldTwice_ReturnsFailure()
    {
        var query = new SearchQuery { Index = $"highlight-{Guid.NewGuid():N}" };
        query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name" });
        query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name" });

        await ShouldFailAsync(query, "names field Name more than once");
    }

    [Fact]
    public async Task SearchAsync_SameFieldUnderItsEngineName_ReturnsFailure()
    {
        var query = new SearchQuery { Index = $"highlight-{Guid.NewGuid():N}" };
        query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name" });
        query.HighlightQueries.Add(new BaseHighlightQuery { Field = "fields.Name" });

        await ShouldFailAsync(query, "names field Name more than once, as field fields.Name");
    }

    [Fact]
    public async Task SearchAsync_NullClauseInOwnHighlightQuery_ReturnsFailure()
    {
        var query = new SearchQuery { Index = $"highlight-{Guid.NewGuid():N}" };
        query.HighlightQueries.Add(new BaseHighlightQuery { Field = "Name", Queries = { [Occurance.MUST] = null! } });

        await ShouldFailAsync(query, "The highlight query for field Name holds a null MUST clause");
    }

    [Fact]
    public async Task SearchAsync_NullHighlightQuery_ReturnsFailure()
    {
        var query = new SearchQuery { Index = $"highlight-{Guid.NewGuid():N}" };
        query.HighlightQueries.Add(null!);

        await ShouldFailAsync(query, "HighlightQueries holds a null entry");
    }

    [Fact]
    public void Highlights_HitWithoutHighlights_CannotBeChangedForOtherHits()
    {
        var hit = new SearchHit(new BaseSearchModel(), null);
        var other = new SearchHit(new BaseSearchModel(), null);

        Should.Throw<NotSupportedException>(() =>
            ((IDictionary<string, IReadOnlyList<string>>)hit.Highlights).Add("Name", ["<em>x</em>"]));

        other.Highlights.ShouldBeEmpty();
    }

    [Fact]
    public void HighLighterConfigurator_New_AddsTheRequestItConfigures()
    {
        var query = new SearchQuery();

        var configurator = new HighLighterConfigurator(query);
        configurator.Query.Field = "Name";

        query.HighlightQueries.ShouldHaveSingleItem().ShouldBeSameAs(configurator.Query);
        query.HighlightQueries[0].Field.ShouldBe("Name");
    }
}
