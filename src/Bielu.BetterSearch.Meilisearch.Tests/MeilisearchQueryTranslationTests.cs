using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;

namespace Bielu.BetterSearch.Meilisearch.Tests;

internal sealed class MeilisearchQueryTranslationTests(IEnumerable<ISubQueryTranslator<MeilisearchFilter>> translators)
    : SubQueryTranslationTestBase<MeilisearchFilter>(translators)
{
    protected override string Render(MeilisearchFilter translated) => translated.Expression;

    // Meilisearch prefix filters only take string values, so a number fails instead of being formatted.
    public override async Task PrefixSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryTranslationShouldFailAsync(new PrefixSubQuery { Field = "Name", Value = 3.5 });

    public override async Task PrefixPhraseSubQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryTranslationShouldFailAsync(new PrefixPhraseSubQuery { Field = "Name", Value = 3.5 });

    public override async Task FuzzyQuery_CommaDecimalCulture_SendsInvariantValue()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Name", Value = 3.5 });

    public override async Task FuzzyQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new FuzzyQuery { Field = "Name", Value = "jnae" });

    public override async Task LuceneSubQueryShouldBeTranslatable()
        => await SubQueryTranslationShouldFailAsync(new LuceneSubQuery { LuceneQuery = "Name:jane" });
}
