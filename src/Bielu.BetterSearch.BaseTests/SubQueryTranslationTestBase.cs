using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;

namespace Bielu.BetterSearch.Tests;

public abstract class SubQueryTranslationTestBase<T>(IEnumerable<ISubQueryTranslator<T>> translators)
{
    private async Task SubQueryShouldBeTranslatable(ISearchSubQuery subQuery)
    {
        var canTranslate = translators.Any(t => t.CanTranslate(subQuery));
        canTranslate.Should().BeTrue($"because a translator should be able to translate {subQuery.GetType().Name}");

        var translator = translators.First(t => t.CanTranslate(subQuery));
        var result = await translator.TranslateAsync(subQuery,translators);
        result.IsSuccess.Should().BeTrue($"because translation of {subQuery.GetType().Name} should be successful");
    }

    protected async Task SubQueryTranslationShouldFail(ISearchSubQuery subQuery)
    {
        var canTranslate = translators.Any(t => t.CanTranslate(subQuery));
        canTranslate.Should().BeTrue($"because a translator should exist for {subQuery.GetType().Name} even if unsupported");

        var translator = translators.First(t => t.CanTranslate(subQuery));
        var result = await translator.TranslateAsync(subQuery, translators);
        result.IsFailed.Should().BeTrue($"because {subQuery.GetType().Name} is not supported by this provider");
    }

    [Fact]
    public virtual async Task BoolSearchQueryShouldBeTranslatable()
    {
        var subQuery = new BoolSearchSubQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task DateRangeQueryShouldBeTranslatable()
    {
        var subQuery = new DateRangeQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task FuzzyQueryShouldBeTranslatable()
    {
        var subQuery = new FuzzyQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task LongRangeShouldBeTranslatable()
    {
        var subQuery = new LongRange();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task NumericRangeShouldBeTranslatable()
    {
        var subQuery = new NumericRange();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task PrefixPhraseSubQueryShouldBeTranslatable()
    {
        var subQuery = new PrefixPhraseSubQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task PrefixSubQueryShouldBeTranslatable()
    {
        var subQuery = new PrefixSubQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task SpatialSearchQueryShouldBeTranslatable()
    {
        var subQuery = new SpatialSearchQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task StringRangeShouldBeTranslatable()
    {
        var subQuery = new StringRange();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task TermsSubQueryShouldBeTranslatable()
    {
        var subQuery = new TermsSubQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task TermSubQueryShouldBeTranslatable()
    {
        var subQuery = new TermSubQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public virtual async Task LuceneSubQueryShouldBeTranslatable()
    {
        var subQuery = new LuceneSubQuery();
        await SubQueryShouldBeTranslatable(subQuery);
    }
}
