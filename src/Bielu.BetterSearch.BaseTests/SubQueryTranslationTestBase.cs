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
    private async Task<Result<T>> TranslateAsync(ISearchSubQuery subQuery)
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
    }

    protected async Task SubQueryTranslationShouldFailAsync(ISearchSubQuery subQuery)
    {
        var result = await TranslateAsync(subQuery);
        result.IsFailed.ShouldBeTrue($"because {subQuery.GetType().Name} is not supported by this provider");
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

    private async Task SubQueryShouldSendInvariantValueAsync(ISearchSubQuery subQuery)
    {
        var rendered = await RenderUnderCommaDecimalCultureAsync(subQuery);
        rendered.ShouldContain("3.5");
        rendered.ShouldNotContain("3,5");
    }

    protected static readonly DateTime Date = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    private async Task SubQueryShouldSendInvariantDateAsync(ISearchSubQuery subQuery)
    {
        var rendered = await RenderUnderCommaDecimalCultureAsync(subQuery);
        rendered.ShouldContain("11/08/2026");
        rendered.ShouldNotContain("08.11.2026");
    }

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
        => await SubQueryShouldSendInvariantValueAsync(new FuzzyQuery { Field = "Name", Value = 3.5 });

    [Fact]
    public virtual async Task PrefixPhraseSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantValueAsync(new PrefixPhraseSubQuery { Field = "Name", Value = 3.5 });

    [Fact]
    public virtual async Task PrefixSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantValueAsync(new PrefixSubQuery { Field = "Name", Value = 3.5 });

    [Fact]
    public virtual async Task TermSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantValueAsync(new TermSubQuery { Field = "Name", Value = 3.5 });

    [Fact]
    public virtual async Task TermsSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryShouldSendInvariantValueAsync(new TermsSubQuery { Field = "Name", Value = [3.5] });

    [Fact]
    public virtual async Task FuzzyQuery_DateValue_SendsInvariantDate()
        => await SubQueryShouldSendInvariantDateAsync(new FuzzyQuery { Field = "Born", Value = Date });

    [Fact]
    public virtual async Task PrefixSubQuery_DateValue_SendsInvariantDate()
        => await SubQueryShouldSendInvariantDateAsync(new PrefixSubQuery { Field = "Born", Value = Date });

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
