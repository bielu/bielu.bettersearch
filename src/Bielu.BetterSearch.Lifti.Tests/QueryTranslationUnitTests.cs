using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationUnitTests(IEnumerable<ISubQueryTranslator<IQuery>> translators) : SubQueryTranslationTestBase<IQuery>(translators)
{
    [Fact]
    public override async Task DateRangeQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFail(new DateRangeQuery());

    [Fact]
    public override async Task LongRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFail(new LongRange());

    [Fact]
    public override async Task NumericRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFail(new NumericRange());

    [Fact]
    public override async Task SpatialSearchQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFail(new SpatialSearchQuery());

    [Fact]
    public override async Task StringRangeShouldBeTranslatable()
        => await SubQueryTranslationShouldFail(new StringRange());

    [Fact]
    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFail(new LuceneSubQuery());
}
