using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services.Queries;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiQueryTranslatorService(
    IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators) : IQueryTranslateServiceAsync<IQuery>
{
    public async Task<Result<IQuery>> TranslateMainQueryAsync(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        if (query.Query == null || query.Query.Count == 0)
        {
            return Result.Ok<IQuery>(Query.Empty);
        }

        var combined = await LiftiBoolQueryBuilder.BuildAsync(
            query.Query.SelectMany(group => group.Value.Select(subQuery => (group.Key, subQuery))),
            subQueryTranslators).ConfigureAwait(false);
        if (combined.IsFailed)
        {
            return combined.ToResult<IQuery>();
        }

        return Result.Ok<IQuery>(combined.Value == null ? Query.Empty : new Query(combined.Value));
    }
}
