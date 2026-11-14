using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Lifti.Querying;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationUnitTests(IEnumerable<ISubQueryTranslator<IQuery>> translators) : SubQueryTranslationTestBase<IQuery>(translators)
{
    protected override string Render(IQuery translated) => translated.ToString() ?? string.Empty;

    // Lifti splits a value into words as the index does, so 3.5 is sent as the words 3 and 5, and so would a
    // culture's 3,5 be. A letter decimal separator keeps the culture's 3x5 as one word, so it shows if it leaks.
    private async Task<string> RenderUnderLetterDecimalCultureAsync(ISearchSubQuery subQuery)
    {
        using var _ = new CultureScope(culture => culture.NumberFormat.NumberDecimalSeparator = "x");
        var result = await TranslateAsync(subQuery);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return Render(result.Value);
    }

    private async Task<string> RenderUnderCommaDecimalCultureAsync(ISearchSubQuery subQuery)
    {
        using var _ = CultureScope.CommaDecimal();
        var result = await TranslateAsync(subQuery);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return Render(result.Value);
    }

    [Fact]
    public override async Task FuzzyQuery_CommaDecimalCulture_SendsInvariantValue()
        => (await RenderUnderLetterDecimalCultureAsync(new FuzzyQuery { Field = "Name", Value = 3.5 })).ShouldBe("\"?2,?3 ?2,?5\"");

    [Fact]
    public override async Task PrefixPhraseSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => (await RenderUnderLetterDecimalCultureAsync(new PrefixPhraseSubQuery { Field = "Name", Value = 3.5 })).ShouldBe("\"3 5*\"");

    [Fact]
    public override async Task PrefixSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => (await RenderUnderLetterDecimalCultureAsync(new PrefixSubQuery { Field = "Name", Value = 3.5 })).ShouldBe("\"3 5*\"");

    [Fact]
    public override async Task TermSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => (await RenderUnderLetterDecimalCultureAsync(new TermSubQuery { Field = "Name", Value = 3.5 })).ShouldBe("\"3 5\"");

    [Fact]
    public override async Task TermsSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => (await RenderUnderLetterDecimalCultureAsync(new TermsSubQuery { Field = "Name", Value = [3.5] })).ShouldBe("\"3 5\"");

    // Lifti matches a fuzzy date or boolean as the words of its text, as indexed, rather than failing, so the
    // shared tests that expect a failure are replaced by the ones below. The offset is converted, so the
    // DateTimeOffset matches the same words as the UTC date.
    private const string FuzzyMatchesWords = "Lifti translates a fuzzy date or boolean as the words of its text; see the FuzzyQuery_*Value_MatchesWordsOfItsText tests";

    [Fact(Skip = FuzzyMatchesWords)]
    public override Task FuzzyQuery_DateValue_ReturnsFailedResult() => Task.CompletedTask;

    [Fact(Skip = FuzzyMatchesWords)]
    public override Task FuzzyQuery_DateTimeOffsetValue_ReturnsFailedResult() => Task.CompletedTask;

    [Fact(Skip = FuzzyMatchesWords)]
    public override Task FuzzyQuery_BooleanValue_ReturnsFailedResult() => Task.CompletedTask;

    [Fact]
    public async Task FuzzyQuery_DateValue_MatchesWordsOfItsText()
        => (await RenderUnderCommaDecimalCultureAsync(new FuzzyQuery { Field = "Born", Value = Date }))
            .ShouldBe("\"?2,?2026 ?2,?11 ?2,?08T00 ?2,?00 ?2,?00Z\"");

    [Fact]
    public async Task FuzzyQuery_DateTimeOffsetValue_MatchesWordsOfItsText()
        => (await RenderUnderCommaDecimalCultureAsync(new FuzzyQuery { Field = "Born", Value = new DateTimeOffset(Date) }))
            .ShouldBe("\"?2,?2026 ?2,?11 ?2,?08T00 ?2,?00 ?2,?00Z\"");

    [Fact]
    public async Task FuzzyQuery_BooleanValue_MatchesWordsOfItsText()
        => (await RenderUnderCommaDecimalCultureAsync(new FuzzyQuery { Field = "Born", Value = true })).ShouldBe("?2,?TRUE");

    [Fact]
    public override async Task TermSubQuery_DateValue_SendsIsoDateInUtc()
        => (await RenderUnderCommaDecimalCultureAsync(new TermSubQuery
        {
            Field = "Born",
            Value = new DateTimeOffset(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1))
        })).ShouldBe("\"2026 11 08T00 00 00 12Z\"");

    [Fact]
    public override async Task TermsSubQuery_DateValue_SendsIsoDateInUtc()
        => (await RenderUnderCommaDecimalCultureAsync(new TermsSubQuery
        {
            Field = "Born",
            Value = [new DateTimeOffset(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1))]
        })).ShouldBe("\"2026 11 08T00 00 00 12Z\"");

    [Fact]
    public override async Task PrefixSubQuery_DateValue_SendsIsoDateInUtc()
        => (await RenderUnderCommaDecimalCultureAsync(new PrefixSubQuery { Field = "Born", Value = Date })).ShouldBe("\"2026 11 08T00 00 00Z*\"");

    [Fact]
    public override async Task PrefixPhraseSubQuery_DateValue_SendsIsoDateInUtc()
        => (await RenderUnderCommaDecimalCultureAsync(new PrefixPhraseSubQuery
        {
            Field = "Born",
            Value = new DateTimeOffset(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1))
        })).ShouldBe("\"2026 11 08T00 00 00 12Z*\"");

    [Fact]
    public override async Task DateRangeQueryShouldBeTranslatable()
        => await RangeShouldBeUnsupportedAsync(new DateRangeQuery { Field = "Born", MinValue = Date }, "date");

    [Fact]
    public override async Task LongRangeShouldBeTranslatable()
        => await RangeShouldBeUnsupportedAsync(new LongRange { Field = "Age", MinValue = 1, MaxValue = 100 }, "long");

    [Fact]
    public override async Task NumericRangeShouldBeTranslatable()
        => await RangeShouldBeUnsupportedAsync(new NumericRange { Field = "Age", MinValue = 1, MaxValue = 100 }, "numeric");

    [Fact]
    public override async Task SpatialSearchQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new SpatialSearchQuery());

    [Fact]
    public override async Task StringRangeShouldBeTranslatable()
        => await RangeShouldBeUnsupportedAsync(new StringRange { Field = "Name", MinValue = "a", MaxValue = "m" }, "string");

    [Fact]
    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LuceneSubQuery());

    // A bounded range, so the failure is the unsupported one rather than the missing bounds.
    private async Task RangeShouldBeUnsupportedAsync(ISearchSubQuery subQuery, string kind)
    {
        var result = await TranslateAsync(subQuery);
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe($"Lifti does not support {kind} range queries.");
    }
}
