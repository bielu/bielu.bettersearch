using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Tests;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Tests;

public class QueryTranslationUnitTests(IEnumerable<ISubQueryTranslator<IQuery>> translators) : SubQueryTranslationTestBase<IQuery>(translators)
{
    protected override string Render(IQuery translated) => translated.ToString() ?? string.Empty;

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
