using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

/// <summary>
/// Lifti does not support numeric range queries. This translator always returns a failure result.
/// See the feature matrix for details on supported features.
/// </summary>
public class NumericRangeSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is NumericRange;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
        => Task.FromResult(Result.Fail<IQuery>("Lifti does not support numeric range queries."));
}
