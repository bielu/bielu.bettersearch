using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;
using Lifti.Querying.QueryParts;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

public class TermSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is TermSubQuery;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not TermSubQuery termQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a term query"));
        }

        var value = (termQuery.Value?.ToString() ?? string.Empty).ToUpperInvariant();
        return Task.FromResult(Result.Ok<IQuery>(new Query(new ExactWordQueryPart(value))));
    }
}
