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

    // A day-first culture would send the words 08 11 2026 instead.
    [Fact]
    public override async Task FuzzyQuery_DateValue_SendsInvariantDate()
        => (await RenderUnderCommaDecimalCultureAsync(new FuzzyQuery { Field = "Born", Value = Date }))
            .ShouldBe("\"?2,?11 ?2,?08 ?2,?2026 ?2,?00 ?2,?00 ?2,?00\"");

    [Fact]
    public override async Task PrefixSubQuery_DateValue_SendsInvariantDate()
        => (await RenderUnderCommaDecimalCultureAsync(new PrefixSubQuery { Field = "Born", Value = Date })).ShouldBe("\"11 08 2026 00 00 00*\"");

    [Fact]
    public override async Task DateRangeQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new DateRangeQuery());

    [Fact]
    public override async Task LongRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LongRange());

    [Fact]
    public override async Task NumericRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new NumericRange());

    [Fact]
    public override async Task SpatialSearchQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new SpatialSearchQuery());

    [Fact]
    public override async Task StringRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new StringRange());

    [Fact]
    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LuceneSubQuery());
}
