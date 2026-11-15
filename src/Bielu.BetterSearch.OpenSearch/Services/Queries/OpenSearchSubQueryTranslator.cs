using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
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

    public Task<Result<QueryContainer>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    {
        if (query is not TSubQuery subQuery)
        {
            return Task.FromResult(Result.Fail<QueryContainer>($"Query is not a {typeof(TSubQuery).Name}"));
        }

        // Every clause, a nested bool included, is translated through here, so the boost is checked once for all of them.
        var boost = Boosts.Check(query, "OpenSearch");
        return boost.IsFailed
            ? Task.FromResult(boost.ToResult<QueryContainer>())
            : TranslateAsync(subQuery, subQueryTranslators);
    }

    protected abstract Task<Result<QueryContainer>> TranslateAsync(TSubQuery query, IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators);
}
