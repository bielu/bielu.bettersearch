using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.Aggregations;
using Bielu.BetterSearch.Abstractions.Query.Highlighter;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;
using static Bielu.BetterSearch.Tests.SearchTestHelpers;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>Runs queries against the in-memory Lifti index and checks what comes back.</summary>
public sealed class LiftiSearchTests(ISearchServiceAsync searchService, IIndexingProviderAsync indexingProvider)
{
    private sealed class SortField : ISortOrderField
    {
        public string FieldName { get; set; } = string.Empty;
        public bool Desc { get; set; }
        public int Order { get; set; }
    }

    private sealed class UnknownSubQuery : ISearchSubQuery
    {
        public Occurance Occurance { get; set; }
        public int BoostValue { get; set; }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SearchDocument Person(string index, string id, string name, int age, string city) => new()
    {
        Id = id,
        Type = "Person",
        Index = index,
        Fields = new Dictionary<string, IList<object>>
        {
            { "Name", [name] },
            { "Age", [age] },
            { "City", [city] }
        }
    };

    /// <summary>Indexes five people in an index of their own and returns its name.</summary>
    private async Task<string> CreatePeopleAsync()
    {
        var index = $"search-{Guid.NewGuid():N}";
        var people = new[]
        {
            Person(index, "Up-1", "Jane Doe", 25, "Warsaw"),
            Person(index, "2", "John Doe", 30, "Krakow"),
            Person(index, "3", "John Smith", 35, "Warsaw"),
            Person(index, "4", "Anna Nowak", 40, "Gdansk"),
            Person(index, "5", "Piotr Kowalski", 45, "Jane")
        };
        (await indexingProvider.IndexMultipleDocumentsAsync(people, Token)).Value.ShouldBe(people.Length);
        return index;
    }

    private Task<List<string>> SearchIdsAsync(SearchQuery query) => searchService.SearchIdsAsync(query, Token);

    [Fact]
    public async Task SearchAsync_NoClauses_ReturnsNothing()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(new SearchQuery { Index = index }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.Pagination.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyClauseList_ReturnsNothing()
    {
        var index = await CreatePeopleAsync();
        var query = new SearchQuery { Index = index };
        query.Query[Occurance.MUST] = [];

        var ids = await SearchIdsAsync(query);

        ids.ShouldBeEmpty();
    }

    /// <summary>Known defect: searching a missing index creates it, where the other providers return a failed <c>Result</c>.</summary>
    [Fact]
    public async Task SearchAsync_MissingIndex_CreatesItAndReturnsNothing()
    {
        var index = $"missing-{Guid.NewGuid():N}";

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jane" }));

        ids.ShouldBeEmpty();
        (await indexingProvider.IndexExistsAsync(index, Token)).Value.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_TermQuery_MatchesTheWordIgnoringCase()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "DOE" }));

        ids.ShouldBe(["2", "Up-1"]);
    }

    /// <summary>Known defect: Lifti ignores <c>Field</c>, so "Jane" also matches the city named Jane.</summary>
    [Fact]
    public async Task SearchAsync_TermQuery_IgnoresTheField()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "Jane" }));

        ids.ShouldBe(["5", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_TermQueryWithNumber_MatchesTheValue()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Age", Value = 30 }));

        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_TermQueryWithPartOfAWord_MatchesNothing()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jan" }));

        ids.ShouldBeEmpty();
    }

    /// <summary>Known defect: terms are upper-cased by hand, while the index folds accents, so an accented term never matches.</summary>
    [Fact]
    public async Task SearchAsync_TermQueryWithAccents_MatchesNothing()
    {
        var index = $"search-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(new SearchDocument
        {
            Id = "1",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Name", ["Żółw Café"] } }
        }, Token)).IsSuccess.ShouldBeTrue();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "żółw" }));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TermsQuery_MatchesAnyTerm()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "Name", Value = ["anna", "smith"] }));

        ids.ShouldBe(["3", "4"]);
    }

    [Fact]
    public async Task SearchAsync_TermsQueryWithNoValues_MatchesNothing()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "Name", Value = [] }));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TermsQueryWithNullValues_MatchesNothing()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermsSubQuery { Field = "Name", Value = null }));

        ids.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FuzzyQuery_MatchesWithinTwoEditsByDefault()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new FuzzyQuery { Field = "Name", Value = "smiht" }));

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_FuzzyQueryWithFuziness_UsesTheLargerBound()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new FuzzyQuery { Field = "Name", Value = "nowek", Fuziness = Tuple.Create(0, 1) }));

        ids.ShouldBe(["4"]);
    }

    [Fact]
    public async Task SearchAsync_FuzzyQueryWithNoEdits_MatchesOnlyTheWord()
    {
        var index = await CreatePeopleAsync();

        var exact = await SearchIdsAsync(Query(index, Occurance.MUST, new FuzzyQuery { Field = "Name", Value = "nowak", Fuziness = Tuple.Create(0, 0) }));
        var typo = await SearchIdsAsync(Query(index, Occurance.MUST, new FuzzyQuery { Field = "Name", Value = "nowek", Fuziness = Tuple.Create(0, 0) }));

        exact.ShouldBe(["4"]);
        typo.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_PrefixQuery_MatchesWordsStartingWithTheValue()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "Jo" }));

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_PrefixPhraseQueryOfOneWord_MatchesWordsStartingWithTheValue()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixPhraseSubQuery { Field = "Name", Value = "kowa" }));

        ids.ShouldBe(["5"]);
    }

    /// <summary>Known defect: the whole value becomes one wildcard over a single word, so a value of many words matches nothing.</summary>
    [Fact]
    public async Task SearchAsync_PrefixPhraseQueryOfManyWords_MatchesNothing()
    {
        var index = await CreatePeopleAsync();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new PrefixPhraseSubQuery { Field = "Name", Value = "jane d" }));

        ids.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(UnsupportedSubQueries))]
    public async Task SearchAsync_UnsupportedQueryType_ReturnsFailedResult(ISearchSubQuery subQuery, string message)
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, subQuery), Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(message);
    }

    public static TheoryData<ISearchSubQuery, string> UnsupportedSubQueries() => new()
    {
        { new DateRangeQuery { Field = "Born", MinValue = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, "date range" },
        { new LongRange { Field = "Age", MinValue = 1, MaxValue = 100 }, "range" },
        { new NumericRange { Field = "Age", MinValue = 1, MaxValue = 100 }, "range" },
        { new StringRange { Field = "Name", MinValue = "a", MaxValue = "m" }, "range" },
        { new SpatialSearchQuery { Field = "Location", Distance = 10, Lat = 52.23, Lng = 21.01 }, "spatial" },
        { new LuceneSubQuery { LuceneQuery = "Name:jane" }, "Lucene" }
    };

    [Fact]
    public async Task SearchAsync_UnsupportedQueryTypeInsideNestedBool_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        nested.Add(Occurance.SHOULD, new NumericRange { Field = "Age", MinValue = 1 });

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, nested), Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("does not support numeric range");
    }

    [Fact]
    public async Task SearchAsync_SubQueryWithoutTranslator_ReturnsFailedResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new UnknownSubQuery()), Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain(nameof(UnknownSubQuery));
    }

    [Fact]
    public async Task SearchAsync_MustClauses_ReturnTheIntersection()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        query.Add(Occurance.MUST, new TermSubQuery { Field = "City", Value = "warsaw" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_FilterClause_IsRequiredAsMust()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.FILTER, new TermSubQuery { Field = "City", Value = "warsaw" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "anna" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_MustAndMustNot_ExcludesTheMatches()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "City", Value = "warsaw" });
        query.Add(Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "doe" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_ShouldAndMustNot_ExcludesFromTheShouldMatches()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "john" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "anna" });
        query.Add(Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "smith" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "4"]);
    }

    [Fact]
    public async Task SearchAsync_NestedBoolOfShould_RequiresAnyOfThem()
    {
        var index = await CreatePeopleAsync();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "anna" });
        nested.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "smith" });
        var query = Query(index, Occurance.MUST, nested);
        query.Add(Occurance.MUST, new TermSubQuery { Field = "City", Value = "warsaw" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["3"]);
    }

    [Fact]
    public async Task SearchAsync_EmptyNestedBool_IsIgnored()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new BoolSearchSubQuery());
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2", "3"]);
    }

    [Fact]
    public async Task SearchAsync_DeeplyNestedBool_KeepsEachLevel()
    {
        var index = await CreatePeopleAsync();
        var inner = new BoolSearchSubQuery();
        inner.Add(Occurance.SHOULD, new TermSubQuery { Field = "City", Value = "krakow" });
        inner.Add(Occurance.SHOULD, new TermSubQuery { Field = "City", Value = "gdansk" });
        var outer = new BoolSearchSubQuery();
        outer.Add(Occurance.MUST, inner);
        outer.Add(Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "anna" });
        var query = Query(index, Occurance.MUST, outer);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_TermsInsideNestedMustNot_ExcludesEveryTerm()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "j" });
        // A nested bool holding only the exclusion, the exclusion-only branch of the bool builder.
        var exclusion = new BoolSearchSubQuery();
        exclusion.Add(Occurance.MUSTNOT, new TermsSubQuery { Field = "Name", Value = ["smith", "jane"] });
        query.Add(Occurance.MUST, exclusion);

        var ids = await SearchIdsAsync(query);

        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_PageAndPageSize_AreIgnored()
    {
        var index = await CreatePeopleAsync();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "City", Value = "warsaw" });
        query.Page = 2;
        query.PageSize = 1;

        var result = await searchService.SearchAsync(query, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count().ShouldBe(2);
        result.Value.Pagination.TotalCount.ShouldBe(2);
        result.Value.Pagination.Page.ShouldBe(0);
        result.Value.Pagination.PageSize.ShouldBe(0);
    }

    /// <summary>Hits stay in score order: the younger document scores higher, so honoring <c>Age DESC</c> would put the older one first.</summary>
    [Fact]
    public async Task SearchAsync_SortOrder_IsIgnored()
    {
        var index = $"search-{Guid.NewGuid():N}";
        (await indexingProvider.IndexMultipleDocumentsAsync(
        [
            new SearchDocument { Id = "older", Index = index, Fields = new Dictionary<string, IList<object>> { { "Name", ["doe and some other words here"] }, { "Age", [60] } } },
            new SearchDocument { Id = "younger", Index = index, Fields = new Dictionary<string, IList<object>> { { "Name", ["doe doe doe"] }, { "Age", [20] } } }
        ], Token)).IsSuccess.ShouldBeTrue();
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" });
        query.SortOrder = [new SortField { FieldName = "Age", Desc = true }];

        var result = await searchService.SearchAsync(query, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["younger", "older"]);
    }

    /// <summary>
    /// The post filter would exclude <c>Up-1</c> (not in Krakow) if it were applied, and the hits, their order and
    /// their scores match the same query without these options. The result model has no facets or highlights to carry.
    /// </summary>
    [Fact]
    public async Task SearchAsync_FacetsHighlightingAndPostFilter_AreIgnored()
    {
        var index = await CreatePeopleAsync();
        var plain = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" }), Token);
        var query = Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" });
        query.FacetQueries = [new TermAggregation { AggregationName = "cities", TermFieldName = "City" }];
        query.HighlightQueries = [new BaseHighlightQuery { Field = "Name" }];
        var krakow = new BoolSearchSubQuery();
        krakow.Add(Occurance.MUST, new TermSubQuery { Field = "City", Value = "krakow" });
        query.PostFilterQuery[Occurance.MUST] = [krakow];

        var result = await searchService.SearchAsync(query, Token);

        plain.IsSuccess.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).Order(StringComparer.Ordinal).ShouldBe(["2", "Up-1"]);
        result.Value.Items.Select(item => (item.Document.Id, item.Score))
            .ShouldBe(plain.Value.Items.Select(item => (item.Document.Id, item.Score)));
        result.Value.Pagination.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_Hits_ComeInScoreOrder()
    {
        var index = $"search-{Guid.NewGuid():N}";
        (await indexingProvider.IndexMultipleDocumentsAsync(
        [
            new SearchDocument { Id = "once", Index = index, Fields = new Dictionary<string, IList<object>> { { "Name", ["doe and some other words here"] } } },
            new SearchDocument { Id = "thrice", Index = index, Fields = new Dictionary<string, IList<object>> { { "Name", ["doe doe doe"] } } }
        ], Token)).IsSuccess.ShouldBeTrue();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" }), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["thrice", "once"]);
        result.Value.Items.First().Score!.Value.ShouldBeGreaterThan(result.Value.Items.Last().Score!.Value);
    }

    /// <summary>Lifti keeps only the id, so a hit has no type and no fields.</summary>
    [Fact]
    public async Task SearchAsync_Hit_CarriesOnlyTheIdAndScore()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jane" }), Token);

        result.IsSuccess.ShouldBeTrue();
        var hit = result.Value.Items.Single(item => item.Document.Id == "Up-1");
        hit.Score.ShouldNotBeNull();
        hit.Score.Value.ShouldBeGreaterThan(0);
        hit.Document.ContentType.ShouldBeNull();
        hit.Document.CustomProperties.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmptyResult()
    {
        var index = await CreatePeopleAsync();

        var result = await searchService.SearchAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "nobody" }), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.Pagination.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_DocumentRemoved_IsNoLongerFound()
    {
        var index = await CreatePeopleAsync();

        (await indexingProvider.RemoveDocumentAsync("Up-1", index, Token)).IsSuccess.ShouldBeTrue();

        var ids = await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" }));
        ids.ShouldBe(["2"]);
    }

    [Fact]
    public async Task SearchAsync_DocumentReindexed_IsFoundByItsNewValues()
    {
        var index = await CreatePeopleAsync();

        (await indexingProvider.IndexDocumentAsync(Person(index, "Up-1", "Jane Nowak", 25, "Warsaw"), Token)).IsSuccess.ShouldBeTrue();

        (await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "doe" }))).ShouldBe(["2"]);
        (await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "nowak" }))).ShouldBe(["4", "Up-1"]);
    }

    [Fact]
    public async Task SearchAsync_FieldWithManyValues_MatchesAnyOfThem()
    {
        var index = $"search-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(new SearchDocument
        {
            Id = "1",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Tags", ["red", "green"] } }
        }, Token)).IsSuccess.ShouldBeTrue();

        (await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Tags", Value = "green" }))).ShouldBe(["1"]);
        (await SearchIdsAsync(Query(index, Occurance.MUST, new TermSubQuery { Field = "Tags", Value = "red" }))).ShouldBe(["1"]);
    }

    [Fact]
    public async Task SearchAsync_IndexesAreSeparate()
    {
        var first = await CreatePeopleAsync();
        var second = $"search-{Guid.NewGuid():N}";
        (await indexingProvider.IndexDocumentAsync(Person(second, "other", "Jane Other", 1, "Lodz"), Token)).IsSuccess.ShouldBeTrue();

        (await SearchIdsAsync(Query(first, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "other" }))).ShouldBeEmpty();
        (await SearchIdsAsync(Query(second, Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jane" }))).ShouldBe(["other"]);
    }
}
