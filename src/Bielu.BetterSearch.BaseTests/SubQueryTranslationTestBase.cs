using System.Globalization;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Shouldly;

namespace Bielu.BetterSearch.Tests;

// Each query is complete, as a caller would build it; a provider may reject an incomplete one.
public abstract class SubQueryTranslationTestBase<T>(IEnumerable<ISubQueryTranslator<T>> translators)
{
    protected async Task<Result<T>> TranslateAsync(ISearchSubQuery subQuery)
    {
        translators.Any(t => t.CanTranslate(subQuery))
            .ShouldBeTrue($"because a translator should be able to translate {subQuery.GetType().Name}");

        var translator = translators.First(t => t.CanTranslate(subQuery));
        return await translator.TranslateAsync(subQuery, translators);
    }

    private async Task SubQueryShouldBeTranslatableAsync(ISearchSubQuery subQuery)
    {
        var result = await TranslateAsync(subQuery);
        result.IsSuccess.ShouldBeTrue($"because translation of {subQuery.GetType().Name} should be successful");
        ((object?)result.Value).ShouldNotBeNull($"because translation of {subQuery.GetType().Name} should return a query");
    }

    protected async Task SubQueryTranslationShouldFailAsync(ISearchSubQuery subQuery)
    {
        var result = await TranslateAsync(subQuery);
        result.IsFailed.ShouldBeTrue($"because {subQuery.GetType().Name} is not supported by this provider");
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>The translated query as text, as it is sent to the engine, so a test can check the values it carries.</summary>
    protected abstract string Render(T translated);

    /// <summary>
    /// Translates and renders under a culture that writes 3.5 as "3,5" and dates day-first, so a
    /// culture-dependent value shows in the result.
    /// </summary>
    private async Task<string> RenderUnderCommaDecimalCultureAsync(ISearchSubQuery subQuery)
    {
        using var _ = CultureScope.CommaDecimal();
        3.5.ToString(CultureInfo.CurrentCulture).ShouldBe("3,5", "because the test culture should write a comma decimal");

        // Rendering stays inside the scope, so a value formatted late, while serializing, is caught too.
        var result = await TranslateAsync(subQuery);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors.Select(error => error.Message)));
        return Render(result.Value);
    }

    private async Task SubQueryShouldSendInvariantAsync(ISearchSubQuery subQuery, string expected, string forbidden)
    {
        var rendered = await RenderUnderCommaDecimalCultureAsync(subQuery);
        rendered.ShouldContain(expected);
        rendered.ShouldNotContain(forbidden);
    }

    protected static readonly DateTime Date = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public virtual async Task BoolSearchQueryShouldBeTranslatable()
    {
        var subQuery = new BoolSearchSubQuery();
        subQuery.Add(Occurance.MUST, new TermSubQuery { Field = "Name", Value = "jane" });
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task DateRangeQueryShouldBeTranslatable()
    {
        var subQuery = new DateRangeQuery { Field = "Born", MinValue = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task FuzzyQueryShouldBeTranslatable()
    {
        var subQuery = new FuzzyQuery { Field = "Name", Value = "jnae" };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task LongRangeShouldBeTranslatable()
    {
        var subQuery = new LongRange { Field = "Age", MinValue = 1, MaxValue = 100 };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task NumericRangeShouldBeTranslatable()
    {
        var subQuery = new NumericRange { Field = "Age", MinValue = 1, MaxValue = 100 };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task PrefixPhraseSubQueryShouldBeTranslatable()
    {
        var subQuery = new PrefixPhraseSubQuery { Field = "Name", Value = "jane d" };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task PrefixSubQueryShouldBeTranslatable()
    {
        var subQuery = new PrefixSubQuery { Field = "Name", Value = "ja" };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task FuzzyQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantAsync(new FuzzyQuery { Field = "Name", Value = 3.5 }, "3.5", "3,5");

    [Fact]
    public virtual async Task PrefixPhraseSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantAsync(new PrefixPhraseSubQuery { Field = "Name", Value = 3.5 }, "3.5", "3,5");

    [Fact]
    public virtual async Task PrefixSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantAsync(new PrefixSubQuery { Field = "Name", Value = 3.5 }, "3.5", "3,5");

    [Fact]
    public virtual async Task TermSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantAsync(new TermSubQuery { Field = "Name", Value = 3.5 }, "3.5", "3,5");

    [Fact]
    public virtual async Task TermsSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantAsync(new TermsSubQuery { Field = "Name", Value = [3.5] }, "3.5", "3,5");

    // A date or boolean cannot be matched within a few edits, so a provider that sends fuzzy values as text fails it.
    [Fact]
    public virtual async Task FuzzyQuery_DateValue_ReturnsFailedResult()
        => await FuzzyValueShouldFailAsync(Date, "DateTime");

    [Fact]
    public virtual async Task FuzzyQuery_DateTimeOffsetValue_ReturnsFailedResult()
        => await FuzzyValueShouldFailAsync(new DateTimeOffset(Date), "DateTimeOffset");

    [Fact]
    public virtual async Task FuzzyQuery_BooleanValue_ReturnsFailedResult()
        => await FuzzyValueShouldFailAsync(true, "Boolean");

    protected async Task FuzzyValueShouldFailAsync(object value, string typeName)
    {
        var result = await TranslateAsync(new FuzzyQuery { Field = "Born", Value = value });
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain($"is a {typeName}; a fuzzy match needs a string or a number");
    }

    [Fact]
    public virtual async Task PrefixSubQuery_DateValue_SendsIsoDateInUtc()
        => await SubQueryShouldSendInvariantAsync(new PrefixSubQuery { Field = "Born", Value = Date }, "2026-11-08T00:00:00Z", "11/08/2026");

    [Fact]
    public virtual async Task PrefixPhraseSubQuery_DateValue_SendsIsoDateInUtc()
        => await SubQueryShouldSendInvariantAsync(
            new PrefixPhraseSubQuery { Field = "Born", Value = new DateTimeOffset(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1)) },
            "2026-11-08T00:00:00.12Z", "+01:00");

    [Fact]
    public virtual async Task TermSubQuery_DateValue_SendsIsoDateInUtc()
        => await SubQueryShouldSendInvariantAsync(
            new TermSubQuery { Field = "Born", Value = new DateTimeOffset(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1)) },
            "2026-11-08T00:00:00.12Z", "+01:00");

    [Fact]
    public virtual async Task TermsSubQuery_DateValue_SendsIsoDateInUtc()
        => await SubQueryShouldSendInvariantAsync(
            new TermsSubQuery { Field = "Born", Value = [new DateTimeOffset(2026, 11, 8, 1, 0, 0, 120, TimeSpan.FromHours(1))] },
            "2026-11-08T00:00:00.12Z", "+01:00");

    [Fact]
    public virtual async Task SpatialSearchQueryShouldBeTranslatable()
    {
        var subQuery = new SpatialSearchQuery { Field = "Location", Distance = 10, Lat = 52.23, Lng = 21.01 };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task StringRangeShouldBeTranslatable()
    {
        var subQuery = new StringRange { Field = "Name", MinValue = "a", MaxValue = "m" };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task TermsSubQueryShouldBeTranslatable()
    {
        var subQuery = new TermsSubQuery { Field = "Name", Value = ["jane", "john"] };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task TermSubQueryShouldBeTranslatable()
    {
        var subQuery = new TermSubQuery { Field = "Name", Value = "jane" };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }

    [Fact]
    public virtual async Task LuceneSubQueryShouldBeTranslatable()
    {
        var subQuery = new LuceneSubQuery { LuceneQuery = "Name:jane" };
        await SubQueryShouldBeTranslatableAsync(subQuery);
    }
}
