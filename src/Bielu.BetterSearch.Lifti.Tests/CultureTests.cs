using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class CultureTests(ISearchServiceAsync searchService, IIndexingServiceAsync indexingService)
{
    [Fact]
    public async Task IndexDocumentAsync_CultureWithLetterDecimalSeparator_IndexesInvariantText()
    {
        var index = $"culture-{Guid.NewGuid():N}";
        // Lifti splits words on punctuation, so a comma decimal separator can't show in a match. A letter
        // separator keeps the culture's "3x5" as one word, while the invariant "3.5" indexes "3" and "5".
        using (new CultureScope(culture => culture.NumberFormat.NumberDecimalSeparator = "x"))
        {
            var indexed = await indexingService.IndexDocumentAsync(new SearchDocument
            {
                Id = "1",
                Index = index,
                Fields = new Dictionary<string, IList<object>> { { "Price", [3.5] } }
            }, TestContext.Current.CancellationToken);
            indexed.IsSuccess.ShouldBeTrue();

            var query = new SearchQuery { Index = index };
            query.Add(Occurance.MUST, new TermSubQuery { Field = "Price", Value = 5 });
            var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
            result.Value.Items.Select(item => item.Document.Id).ShouldBe(["1"]);
        }
    }

    /// <summary>Indexes each document's value in the field <c>Value</c> of an index of its own and returns its name.</summary>
    private async Task<string> IndexValuesAsync(params (string Id, IList<object> Values)[] documents)
    {
        var index = $"culture-{Guid.NewGuid():N}";
        foreach (var (id, values) in documents)
        {
            var indexed = await indexingService.IndexDocumentAsync(new SearchDocument
            {
                Id = id,
                Index = index,
                Fields = new Dictionary<string, IList<object>> { { "Value", values } }
            }, TestContext.Current.CancellationToken);
            indexed.IsSuccess.ShouldBeTrue();
        }

        return index;
    }

    private Task<List<string>> SearchIdsAsync(string index, ISearchSubQuery subQuery) =>
        searchService.SearchIdsAsync(SearchTestHelpers.Query(index, Occurance.MUST, subQuery), TestContext.Current.CancellationToken);

    [Fact]
    public async Task SearchAsync_TermForWholeDecimal_MatchesTheDecimalOnly()
    {
        // 3.5 is indexed as the words 3 and 5, and the query looks for them next to each other, in that order.
        var index = await IndexValuesAsync(("decimal", [3.5]), ("reversed", [5.3]), ("integer", [35d]));

        var ids = await SearchIdsAsync(index, new TermSubQuery { Field = "Value", Value = 3.5 });

        ids.ShouldBe(["decimal"]);
    }

    [Fact]
    public async Task SearchAsync_FuzzyForDecimal_MatchesTheDecimalNotANearbyInteger()
    {
        // 35 is one edit from 3.5 as text, but each word of 3.5 is matched on its own.
        var index = await IndexValuesAsync(("decimal", [3.5]), ("integer", [35d]));

        var ids = await SearchIdsAsync(index, new FuzzyQuery { Field = "Value", Value = 3.5 });

        ids.ShouldBe(["decimal"]);
    }

    [Fact]
    public async Task SearchAsync_TermForDecimal_DoesNotMatchItsWordsInSeparateValues()
    {
        // ["3", "5"] holds the words 3 and 5 next to each other once joined, but they belong to different values.
        var index = await IndexValuesAsync(("decimal", [3.5]), ("list", ["3", "5"]));

        var term = await SearchIdsAsync(index, new TermSubQuery { Field = "Value", Value = 3.5 });
        var fuzzy = await SearchIdsAsync(index, new FuzzyQuery { Field = "Value", Value = 3.5 });
        var single = await SearchIdsAsync(index, new TermSubQuery { Field = "Value", Value = "5" });

        term.ShouldBe(["decimal"]);
        fuzzy.ShouldBe(["decimal"]);
        single.ShouldBe(["decimal", "list"]);
    }

    [Theory]
    [InlineData("x", null)]
    [InlineData("x", 5)]
    [InlineData("elephant x giraffe", null)]
    public async Task SearchAsync_Fuzzy_DoesNotMatchTheValueSeparator(string value, int? fuzziness)
    {
        // A fuzzy word within a few edits of the separator would match every multi-valued field, alone or as
        // the middle word of a phrase that runs across two values.
        var index = await IndexValuesAsync(("list", ["elephant", "giraffe"]), ("x", ["x"]));

        var ids = await SearchIdsAsync(index, new FuzzyQuery
        {
            Field = "Value",
            Value = value,
            Fuziness = fuzziness is { } edits ? Tuple.Create(edits, edits) : null
        });

        ids.ShouldBe(value == "x" ? ["x"] : []);
    }

    [Fact]
    public async Task SearchAsync_TermForWholeDate_MatchesThatDateOnly()
    {
        var index = await IndexValuesAsync(
            ("date", [new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc)]),
            ("swapped", [new DateTime(2026, 8, 11, 13, 5, 0, DateTimeKind.Utc)]));

        using var _ = CultureScope.CommaDecimal();
        var ids = await SearchIdsAsync(index, new TermSubQuery { Field = "Value", Value = new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc) });

        ids.ShouldBe(["date"]);
    }

    [Fact]
    public async Task SearchAsync_TermForWholeDateTimeOffset_MatchesThatDateOnly()
    {
        var index = await IndexValuesAsync(
            ("utc", [new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero)]),
            ("warsaw", [new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(1))]));

        var ids = await SearchIdsAsync(index, new TermSubQuery { Field = "Value", Value = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero) });

        ids.ShouldBe(["utc"]);
    }

    [Fact]
    public async Task SearchAsync_PrefixOfDate_MatchesDatesStartingWithIt()
    {
        var index = await IndexValuesAsync(
            ("2026", [new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc)]),
            ("1926", [new DateTime(1926, 11, 8, 0, 0, 0, DateTimeKind.Utc)]));

        var ids = await SearchIdsAsync(index, new PrefixSubQuery { Field = "Value", Value = "11/08/20" });

        ids.ShouldBe(["2026"]);
    }

    [Theory]
    [InlineData(1e20)]
    [InlineData(1e-7)]
    [InlineData(-2.5e-12)]
    public async Task SearchAsync_TermForExponent_MatchesThatValueOnly(double value)
    {
        // 1E+20 is one word; 1E-07 is the words 1E and 07, as the tokenizer splits on the dash.
        var index = await IndexValuesAsync(("exponent", [value]), ("other", [value * 10]));

        var ids = await SearchIdsAsync(index, new TermSubQuery { Field = "Value", Value = value });

        ids.ShouldBe(["exponent"]);
    }

    [Fact]
    public async Task SearchAsync_TermForDayOfDate_MatchesDocument()
    {
        // A date is indexed with the invariant culture, "11/08/2026 13:05:00", so each of its parts is a word.
        var index = $"culture-{Guid.NewGuid():N}";
        using var _ = CultureScope.CommaDecimal();
        var indexed = await indexingService.IndexDocumentAsync(new SearchDocument
        {
            Id = "1",
            Index = index,
            Fields = new Dictionary<string, IList<object>> { { "Born", [new DateTime(2026, 11, 8, 13, 5, 0, DateTimeKind.Utc)] } }
        }, TestContext.Current.CancellationToken);
        indexed.IsSuccess.ShouldBeTrue();

        var query = new SearchQuery { Index = index };
        query.Add(Occurance.MUST, new TermSubQuery { Field = "Born", Value = "08" });
        var result = await searchService.SearchAsync(query, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        result.Value.Items.Select(item => item.Document.Id).ShouldBe(["1"]);
    }
}
