using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services.Queries;

/// <summary>
/// Base for the sub-query translators. Translators mark their clauses verbatim: OpenSearch.Client otherwise
/// drops a clause it considers conditionless (an empty term, an unbounded range), which would silently
/// remove the restriction from the query.
/// </summary>
internal abstract class OpenSearchSubQueryTranslator<TSubQuery> : ISubQueryTranslator<QueryContainer>
    where TSubQuery : ISearchSubQuery
{
    public bool CanTranslate(ISearchSubQuery query) => query is TSubQuery;

    public Task<Result<QueryContainer>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators) =>
        query is TSubQuery subQuery
            ? TranslateAsync(subQuery, subQueryTranslators)
            : Task.FromResult(Result.Fail<QueryContainer>($"Query is not a {typeof(TSubQuery).Name}"));

    protected abstract Task<Result<QueryContainer>> TranslateAsync(TSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators);
}
