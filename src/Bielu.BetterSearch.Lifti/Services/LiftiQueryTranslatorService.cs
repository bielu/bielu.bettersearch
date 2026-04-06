using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiQueryTranslatorService(
    IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators) : IQueryTranslateServiceAsync<IQuery>
{
    public async Task<Result<IQuery>> TranslateMainQuery(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        if (query.Query == null || query.Query.Count == 0)
        {
            return Result.Ok<IQuery>(Query.Empty);
        }

        IQueryPart? combined = null;

        foreach (var queryGroup in query.Query)
        {
            var occurance = queryGroup.Key;
            foreach (var subQuery in queryGroup.Value)
            {
                var translator = subQueryTranslators.FirstOrDefault(t => t.CanTranslate(subQuery));
                if (translator == null)
                {
                    return Result.Fail<IQuery>($"No translator found for subquery type {subQuery.GetType().Name}");
                }

                var translationResult = await translator.TranslateAsync(subQuery, subQueryTranslators);
                if (translationResult.IsFailed)
                {
                    return Result.Fail<IQuery>(translationResult.Errors);
                }

                var queryPart = translationResult.Value.Root;
                if (queryPart == null)
                {
                    continue;
                }

                if (combined == null)
                {
                    combined = queryPart;
                }
                else if (occurance == Occurance.FILTER)
                {
                    return Result.Fail<IQuery>("Lifti does not support FILTER occurance.");
                }
                else
                {
                    combined = occurance switch
                    {
                        Occurance.MUST => new AndQueryOperator(combined, queryPart),
                        Occurance.MUSTNOT => new AndNotQueryOperator(combined, queryPart),
                        Occurance.SHOULD => new OrQueryOperator(combined, queryPart),
                        _ => new AndQueryOperator(combined, queryPart)
                    };
                }
            }
        }

        return Result.Ok<IQuery>(new Query(combined));
    }
}
