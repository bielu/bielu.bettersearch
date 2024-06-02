using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentAssertions;

namespace Bielu.BetterSearch.Tests;

public abstract class SubQueryTranslationTestBase<T>(IEnumerable<ISubQueryTranslator<ISearchSubQuery, T>> translators)
{
    private async Task SubQueryShouldBeTranslatable(ISearchSubQuery subQuery)
    {
        var canTranslate = translators.Any(t => t.CanTranslate(subQuery));
        canTranslate.Should().BeTrue($"because a translator should be able to translate {subQuery.GetType().Name}");

        var translator = translators.First(t => t.CanTranslate(subQuery));
        var result = await translator.TranslateAsync(subQuery);
        result.IsSuccess.Should().BeTrue($"because translation of {subQuery.GetType().Name} should be successful");
    }

    [Fact]
    public async Task BoolSearchQueryShouldBeTranslatable()
    {
        var subQuery = new BoolSearchSubQuery(); // Replace with an actual instance of your subquery
        await SubQueryShouldBeTranslatable(subQuery);
    }

    [Fact]
    public async Task DateRangeQueryShouldBeTranslatable()
    {
        var subQuery = new DateRangeQuery(); // Replace with an actual instance of your subquery
        await SubQueryShouldBeTranslatable(subQuery);
    }
    [Fact]
        public async Task FuzzyQueryShouldBeTranslatable()
        {
            var subQuery = new FuzzyQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task LongRangeShouldBeTranslatable()
        {
            var subQuery = new LongRange(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task NumericRangeShouldBeTranslatable()
        {
            var subQuery = new NumericRange(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task PrefixPhraseSubQueryShouldBeTranslatable()
        {
            var subQuery = new PrefixPhraseSubQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task PrefixSubQueryShouldBeTranslatable()
        {
            var subQuery = new PrefixSubQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task SpatialSearchQueryShouldBeTranslatable()
        {
            var subQuery = new SpatialSearchQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task StringRangeShouldBeTranslatable()
        {
            var subQuery = new StringRange(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task TermsSubQueryShouldBeTranslatable()
        {
            var subQuery = new TermsSubQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }

        [Fact]
        public async Task TermSubQueryShouldBeTranslatable()
        {
            var subQuery = new TermSubQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }
        [Fact]
        public async Task LuceneSubQueryShouldBeTranslatable()
        {
            var subQuery = new LuceneSubQuery(); // Replace with an actual instance of your subquery
            await SubQueryShouldBeTranslatable(subQuery);
        }
}
