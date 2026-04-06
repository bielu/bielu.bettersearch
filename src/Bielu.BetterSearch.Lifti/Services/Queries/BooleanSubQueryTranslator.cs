using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

public class BooleanSubQueryTranslator : ISubQueryTranslator<IQuery>
{
#pragma warning disable CA1822
    public bool CanTranslate(ISearchSubQuery query) => query is BoolSearchSubQuery;
#pragma warning restore CA1822

    public async Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not BoolSearchSubQuery boolQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a boolean query")).Result;
        }

        if (boolQuery.NestedQueries.Count == 0)
        {
            return Result.Ok<IQuery>(new Query(EmptyQueryPart.Instance));
        }

        IQueryPart? combined = null;
        foreach (var nested in boolQuery.NestedQueries)
        {
            var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(nested));
            if (translator == null)
            {
                return Result.Fail<IQuery>($"No translator found for nested query type {nested.GetType().Name}");
            }

            var result = await translator.TranslateAsync(nested, subQueryTranslators);
            if (result.IsFailed)
            {
                return result;
            }

            var part = result.Value.Root;
            if (combined == null)
            {
                combined = part;
            }
            else
            {
                combined = nested.Occurance switch
                {
                    Occurance.MUST => new AndQueryOperator(combined, part),
                    Occurance.MUSTNOT => new AndNotQueryOperator(combined, part),
                    Occurance.SHOULD => new OrQueryOperator(combined, part),
                    _ => new AndQueryOperator(combined, part)
                };
            }
        }

        return Result.Ok<IQuery>(new Query(combined));
    }
}
