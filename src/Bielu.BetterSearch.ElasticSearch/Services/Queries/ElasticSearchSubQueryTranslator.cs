using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services.Queries;

internal abstract class ElasticSearchSubQueryTranslator<TSubQuery> : ISubQueryTranslator<EsQuery>
    where TSubQuery : ISearchSubQuery
{
    public bool CanTranslate(ISearchSubQuery query) => query is TSubQuery;

    public Task<Result<EsQuery>> TranslateAsync(ISearchSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators) =>
        query is TSubQuery subQuery
            ? TranslateAsync(subQuery, subQueryTranslators)
            : Task.FromResult(Result.Fail<EsQuery>($"Query is not a {typeof(TSubQuery).Name}"));

    protected abstract Task<Result<EsQuery>> TranslateAsync(TSubQuery query, IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators);
}
