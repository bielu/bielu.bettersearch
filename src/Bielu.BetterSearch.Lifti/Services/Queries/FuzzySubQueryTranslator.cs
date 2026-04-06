using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

public class FuzzySubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is FuzzyQuery;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not FuzzyQuery fuzzyQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a fuzzy query"));
        }

        var value = (fuzzyQuery.Value?.ToString() ?? string.Empty).ToUpperInvariant();
        ushort maxEdits = fuzzyQuery.Fuziness != null
            ? (ushort)Math.Max(fuzzyQuery.Fuziness.Item1, fuzzyQuery.Fuziness.Item2)
            : (ushort)2;
        ushort maxSequential = (ushort)1;

        return Task.FromResult(Result.Ok<IQuery>(new Query(new FuzzyMatchQueryPart(value, maxEdits, maxSequential))));
    }
}
