using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

/// <summary>
/// Lifti does not support numeric range queries. This translator always returns a failure result: one naming the missing bounds when the range has neither, or the bound that is not
/// a finite number, as every provider does, and otherwise one saying the query is not supported.
/// See the feature matrix for details on supported features.
/// </summary>
public class NumericRangeSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is NumericRange;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        // A range with no bounds fails as it does in every provider, so a forgotten bound reads the same everywhere.
        var range = (NumericRange)query;
        var bounds = Result.Merge(
            RangeBounds.Require(nameof(NumericRange), range.MinValue, range.MaxValue),
            RangeBounds.RequireFinite(nameof(NumericRange), range.MinValue, range.MaxValue));
        return Task.FromResult(bounds.IsFailed
            ? bounds.ToResult<IQuery>()
            : Result.Fail<IQuery>("Lifti does not support numeric range queries."));
    }
}
