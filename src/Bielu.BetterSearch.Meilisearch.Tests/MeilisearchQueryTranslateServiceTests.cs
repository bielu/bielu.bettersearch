using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Shouldly;
using MeilisearchQuery = Meilisearch.SearchQuery;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>Pins the filter expressions; the search tests check what they match on a real server.</summary>
public class MeilisearchQueryTranslateServiceTests(IQueryTranslateServiceAsync<MeilisearchQuery> translateService)
{
    private async Task<string> FilterAsync(SearchQuery query)
    {
        var result = await translateService.TranslateMainQueryAsync(query);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return ((object)result.Value.Filter).ShouldBeOfType<string>();
    }

    private static SearchQuery Query(Occurance occurance, ISearchSubQuery subQuery)
    {
        var query = new SearchQuery();
        query.Add(occurance, subQuery);
        return query;
    }

    [Fact]
    public async Task TranslateMainQueryAsync_NoClauses_MatchesEveryDocument()
    {
        (await FilterAsync(new SearchQuery())).ShouldBe("id EXISTS");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_TermQuery_FiltersOnLowerCasedTokens()
    {
        var filter = await FilterAsync(Query(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "Jane \"J\" \\" }));

        filter.ShouldBe("(tokens.Name = \"jane \\\"j\\\" \\\\\")");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_TermQueryOnStoredValue_KeepsTheValueAsGiven()
    {
        var filter = await FilterAsync(Query(Occurance.MUST, new TermSubQuery { Field = "fields.City", Value = "Warsaw" }));

        filter.ShouldBe("(fields.City = \"Warsaw\")");
    }

    [Theory]
    [InlineData(25, "fields.Age = 25")]
    [InlineData(2.5d, "fields.Age = 2.5")]
    [InlineData(true, "fields.Age = true")]
    public async Task TranslateMainQueryAsync_TermQueryWithNonString_ComparesTheStoredValue(object value, string expected)
    {
        var filter = await FilterAsync(Query(Occurance.MUST, new TermSubQuery { Field = "Age", Value = value }));

        filter.ShouldBe($"({expected})");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_TermQueryWithDate_ComparesUnixMilliseconds()
    {
        var filter = await FilterAsync(Query(Occurance.MUST, new TermSubQuery { Field = "Born", Value = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Unspecified) }));

        filter.ShouldBe("(dates.Born = 1000)");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_ShouldBesideMust_KeepsOnlyTheMust()
    {
        var query = Query(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        query.Add(Occurance.SHOULD, new TermSubQuery { Field = "Name", Value = "jane" });

        (await FilterAsync(query)).ShouldBe("(tokens.Name = \"john\")");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_MustNotOnly_ExcludesFromEveryDocument()
    {
        var filter = await FilterAsync(Query(Occurance.MUSTNOT, new TermSubQuery { Field = "Name", Value = "john" }));

        filter.ShouldBe("id EXISTS AND NOT (tokens.Name = \"john\")");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_EmptyTerms_MatchesNothing()
    {
        var filter = await FilterAsync(Query(Occurance.MUST, new TermsSubQuery { Field = "Name", Value = [] }));

        filter.ShouldBe("(id IN [])");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_RangeWithoutBounds_RequiresTheField()
    {
        var filter = await FilterAsync(Query(Occurance.MUST, new NumericRange { Field = "Age" }));

        filter.ShouldBe("(fields.Age EXISTS)");
    }

    [Fact]
    public async Task TranslateMainQueryAsync_PostFilter_IsCombinedWithTheQuery()
    {
        var query = Query(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "john" });
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUST, new TermSubQuery { Field = "City", Value = "krakow" });
        query.PostFilterQuery[Occurance.MUST] = [nested];

        (await FilterAsync(query)).ShouldBe("((tokens.Name = \"john\")) AND (((tokens.City = \"krakow\")))");
    }

    public static TheoryData<ISearchSubQuery> InvalidSubQueries() =>
    [
        new TermSubQuery { Value = "jane" },
        new TermSubQuery { Field = "Name with space", Value = "jane" },
        new TermSubQuery { Field = "Name" },
        new TermSubQuery { Field = "Name", Value = new object() },
        new TermsSubQuery { Field = "Name", Value = ["jane", new object()] },
        new TermsSubQuery { Value = ["jane"] },
        new PrefixSubQuery { Field = "Name", Value = 5 },
        new PrefixSubQuery { Value = "ja" },
        new PrefixPhraseSubQuery { Field = "Name", Value = "  " },
        new PrefixPhraseSubQuery { Value = "jane" },
        new PrefixPhraseSubQuery { Field = "Name", Value = string.Join(' ', Enumerable.Repeat("word", 17)) },
        new NumericRange { MinValue = 1 },
        new FuzzyQuery { Field = "Name", Value = "jnae" },
        new LuceneSubQuery { LuceneQuery = "Name:jane" },
        new SpatialSearchQuery { Field = "Location" },
    ];

    [Theory]
    [MemberData(nameof(InvalidSubQueries))]
    public async Task TranslateMainQueryAsync_UnsupportedOrIncompleteSubQuery_ReturnsFailure(ISearchSubQuery subQuery)
    {
        var result = await translateService.TranslateMainQueryAsync(Query(Occurance.MUST, subQuery));

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task TranslateMainQueryAsync_FailingPostFilter_ReturnsFailure()
    {
        var query = new SearchQuery();
        var nested = new BoolSearchSubQuery();
        nested.Add(Occurance.MUST, new LuceneSubQuery { LuceneQuery = "x" });
        query.PostFilterQuery[Occurance.MUST] = [nested];

        var result = await translateService.TranslateMainQueryAsync(query);

        result.IsFailed.ShouldBeTrue();
    }
}
