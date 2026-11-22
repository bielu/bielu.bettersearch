using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal abstract class ElasticSearchSubQueryTranslator<TSubQuery> : ISubQueryTranslator<EsQuery>
    where TSubQuery : ISearchSubQuery
{
    public bool CanTranslate(ISearchSubQuery query) => query is TSubQuery;

    public Task<Result<EsQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    {
        if (query is not TSubQuery subQuery)
        {
            return Task.FromResult(Result.Fail<EsQuery>($"Query is not a {typeof(TSubQuery).Name}"));
        }

        // Every clause, a nested bool included, is translated through here, so the boost is checked once for all of them.
        var boost = Boosts.Check(query, "Elasticsearch");
        return boost.IsFailed
            ? Task.FromResult(boost.ToResult<EsQuery>())
            : TranslateAsync(subQuery, subQueryTranslators);
    }

    protected abstract Task<Result<EsQuery>> TranslateAsync(TSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators);
}
