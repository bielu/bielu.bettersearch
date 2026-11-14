using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

/// <summary>
/// Lifti does not support range queries. This translator always returns a failure result: one naming the missing bounds when the range has neither, as
/// every provider does, and otherwise one saying the query is not supported.
/// See the feature matrix for details on supported features.
/// </summary>
public class DateRangeSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is DateRangeQuery;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        // A range with no bounds fails as it does in every provider, so a forgotten bound reads the same everywhere.
        var range = (DateRangeQuery)query;
        var bounds = RangeBounds.Require(nameof(DateRangeQuery), range.MinValue, range.MaxValue);
        return Task.FromResult(bounds.IsFailed
            ? bounds.ToResult<IQuery>()
            : Result.Fail<IQuery>("Lifti does not support date range queries."));
    }
}
