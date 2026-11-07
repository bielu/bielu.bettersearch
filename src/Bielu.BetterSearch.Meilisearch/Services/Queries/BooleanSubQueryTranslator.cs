using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Meilisearch.Services.Queries;

/// <summary>Each nested query is a clause of the nested filter, with its own occurrence.</summary>
internal sealed class BooleanSubQueryTranslator : ISubQueryTranslator<MeilisearchFilter>
{
    public bool CanTranslate(ISearchSubQuery query) => query is BoolSearchSubQuery;

    public Task<Result<MeilisearchFilter>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators) =>
        query is BoolSearchSubQuery boolQuery
            ? MeilisearchBoolFilterBuilder.BuildAsync(boolQuery.NestedQueries.Select(nested => (nested.Occurance, nested)), subQueryTranslators)
            : Task.FromResult(Result.Fail<MeilisearchFilter>($"Query is not a {nameof(BoolSearchSubQuery)}"));
}
