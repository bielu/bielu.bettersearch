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

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not BoolSearchSubQuery boolQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a boolean query"));
        }

        // var subQueries = boolQuery.NestedQueries.Select((IGrouping<Occurance, KeyValuePair<Occurance, List<ISearchSubQuery>>> subQuery) => subQueryTranslators.First(translator => translator.CanTranslate(subQuery)).TranslateAsync(subQuery, subQueryTranslators)).ToList();
        //
        // Task.WaitAll(subQueries.ToArray());
        // var results = subQueries.Select(subQuery => subQuery.Result).ToList();
        // if(results.Any(x=>x.IsFailed))  {
        //     return Task.FromResult(Result.Fail<IQuery>("Query translation failed"));
        // }

        return Task.FromResult<Result<IQuery>>(new Query(new AndQueryOperator(new ExactWordQueryPart("test"),new ExactWordQueryPart("test"))));
    }
}
