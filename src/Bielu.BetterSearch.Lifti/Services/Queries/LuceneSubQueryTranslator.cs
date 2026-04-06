using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Lifti.Querying;

namespace Bielu.BetterSearch.Lifti.Services.Queries;

public class LuceneSubQueryTranslator : ISubQueryTranslator<IQuery>
{
    public bool CanTranslate(ISearchSubQuery query) => query is LuceneSubQuery;

    public Task<Result<IQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<IQuery>> subQueryTranslators)
    {
        if (query is not LuceneSubQuery)
        {
            return Task.FromResult(Result.Fail<IQuery>("Query is not a Lucene query"));
        }

        return Task.FromResult(Result.Fail<IQuery>("Lifti does not support raw Lucene query syntax. Use specific query types instead."));
    }
}
