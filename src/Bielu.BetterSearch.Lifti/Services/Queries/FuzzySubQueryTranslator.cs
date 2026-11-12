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

        ushort maxEdits = fuzzyQuery.Fuziness != null
            ? (ushort)Math.Max(fuzzyQuery.Fuziness.Item1, fuzzyQuery.Fuziness.Item2)
            : (ushort)2;
        ushort maxSequential = (ushort)1;

        // Each word is matched fuzzily on its own, so 3.5 matches the words 3 and 5, not the single word 35.
        var part = LiftiQueryWords.Match(fuzzyQuery.Value, "fuzzy", word => new FuzzyMatchQueryPart(word, maxEdits, maxSequential));
        return Task.FromResult(part.IsFailed ? part.ToResult<IQuery>() : Result.Ok<IQuery>(new Query(part.Value)));
    }
}
