using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

public sealed class MeilisearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<MeilisearchFilter>> translators)
    : SubQueryTranslationTestBase<MeilisearchFilter>(translators)
{
    public override async Task FuzzyQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Name", Value = "jnae" });

    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LuceneSubQuery { LuceneQuery = "Name:jane" });

    public override async Task SpatialSearchQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new SpatialSearchQuery { Field = "Location", Distance = 10, Lat = 52.23, Lng = 21.01 });
}
