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

        var combined = await LiftiBoolQueryBuilder.BuildAsync(
            boolQuery.NestedQueries.Select(nested => (nested.Occurance, nested)),
            subQueryTranslators).ConfigureAwait(false);
        if (combined.IsFailed)
        {
            return combined.ToResult<IQuery>();
        }

        return Result.Ok<IQuery>(new Query(combined.Value ?? EmptyQueryPart.Instance));
    }
}
