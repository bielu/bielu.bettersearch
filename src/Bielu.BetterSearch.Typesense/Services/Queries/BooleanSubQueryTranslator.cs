using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services.Queries;

/// <summary>Each nested query is a clause of the nested filter, with its own occurrence.</summary>
internal sealed class BooleanSubQueryTranslator : ISubQueryTranslator<TypesenseFilter>
{
    public bool CanTranslate(ISearchSubQuery query) => query is BoolSearchSubQuery;

    public Task<Result<TypesenseFilter>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators) =>
        query is BoolSearchSubQuery boolQuery
            ? TypesenseBoolFilterBuilder.BuildAsync(boolQuery.NestedQueries.Select(nested => (nested.Occurance, nested)), subQueryTranslators)
            : Task.FromResult(Result.Fail<TypesenseFilter>($"Query is not a {nameof(BoolSearchSubQuery)}"));
}
