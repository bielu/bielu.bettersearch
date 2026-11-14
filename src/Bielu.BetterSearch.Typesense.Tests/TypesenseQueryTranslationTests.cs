using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Bielu.BetterSearch.Typesense.Services;
using Shouldly;

namespace Bielu.BetterSearch.Typesense.Tests;

// Internal, as TypesenseFilter is; its own tests are in TypesenseFilterTranslationTests.
internal sealed class TypesenseQueryTranslationTests(IEnumerable<ISubQueryTranslator<TypesenseFilter>> translators)
    : SubQueryTranslationTestBase<TypesenseFilter>(translators)
{
    private static readonly DateTimeOffset OffsetDate = new(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1));

    // Rendered as if every field the filter names exists.
    protected override string Render(TypesenseFilter translated) => translated.Render(_ => true).Value.Expression ?? string.Empty;

    // Prefix filters only take string values, so a number or date fails instead of being formatted.
    public override async Task PrefixSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryTranslationShouldFailAsync(new PrefixSubQuery { Field = "Name", Value = 3.5 });

    public override async Task PrefixPhraseSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryTranslationShouldFailAsync(new PrefixPhraseSubQuery { Field = "Name", Value = 3.5 });

    public override async Task PrefixSubQuery_DateValue_SendsIsoDateInUtc()
        => await SubQueryTranslationShouldFailAsync(new PrefixSubQuery { Field = "Born", Value = Date });

    public override async Task PrefixPhraseSubQuery_DateValue_SendsIsoDateInUtc()
        => await SubQueryTranslationShouldFailAsync(new PrefixPhraseSubQuery { Field = "Born", Value = OffsetDate });

    // Dates are stored and compared as Unix milliseconds, so the filter carries the instant as a number.
    public override async Task TermSubQuery_DateValue_SendsIsoDateInUtc()
        => await ShouldRenderAsync(new TermSubQuery { Field = "Born", Value = OffsetDate }, $"d_Born:=[{OffsetDate.ToUnixTimeMilliseconds()}]");

    public override async Task TermsSubQuery_DateValue_SendsIsoDateInUtc()
        => await ShouldRenderAsync(new TermsSubQuery { Field = "Born", Value = [OffsetDate] }, $"d_Born:=[{OffsetDate.ToUnixTimeMilliseconds()}]");

    // The provider rejects every FuzzyQuery, whatever its value; see FuzzyQueryShouldBeTranslatable.
    public override async Task FuzzyQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Name", Value = 3.5 });

    public override async Task FuzzyQuery_DateValue_ReturnsFailedResult()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Born", Value = Date });

    public override async Task FuzzyQuery_DateTimeOffsetValue_ReturnsFailedResult()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Born", Value = new DateTimeOffset(Date) });

    public override async Task FuzzyQuery_BooleanValue_ReturnsFailedResult()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Born", Value = true });

    public override async Task FuzzyQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Name", Value = "jnae" });

    public override async Task StringRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new StringRange { Field = "Name", MinValue = "a", MaxValue = "m" });

    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LuceneSubQuery { LuceneQuery = "Name:jane" });


    private async Task ShouldRenderAsync(ISearchSubQuery subQuery, string expected)
    {
        var result = await TranslateAsync(subQuery);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        Render(result.Value).ShouldBe(expected);
    }
}
