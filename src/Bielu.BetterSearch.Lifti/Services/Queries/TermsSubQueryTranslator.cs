using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

public class TermsSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is TermsSubQuery;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not TermsSubQuery termsQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a terms query"));
        }

        if (termsQuery.Value == null || termsQuery.Value.Count == 0)
        {
            return Task.FromResult(Result.Ok<IQuery>(new Query(EmptyQueryPart.Instance)));
        }

        IQueryPart? combined = null;
        foreach (var value in termsQuery.Value)
        {
            var part = LiftiQueryWords.Match(value, "terms", word => new ExactWordQueryPart(word));
            if (part.IsFailed)
            {
                return Task.FromResult(part.ToResult<IQuery>());
            }

            combined = combined == null ? part.Value : new OrQueryOperator(combined, part.Value);
        }

        return Task.FromResult(Result.Ok<IQuery>(new Query(combined)));
    }
}
