using Bielu.BetterSearch.Abstractions.Query.SubQueries;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

internal sealed class FuzzySubQueryTranslator()
    : UnsupportedSubQueryTranslator<FuzzyQuery>("Meilisearch filters only match exact values, and its typo tolerance applies to the whole search text rather than one field");

internal sealed class LuceneSubQueryTranslator()
    : UnsupportedSubQueryTranslator<LuceneSubQuery>("Meilisearch has no Lucene query syntax");
