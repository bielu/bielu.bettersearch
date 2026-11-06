using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Translates the main query into one Elasticsearch <c>bool</c> query, each sub-query a clause under
/// the occurrence it was added with. A query with no clauses matches every document.
/// </summary>
public class ElasticSearchQueryTranslateService(IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    : IQueryTranslateServiceAsync<EsQuery>
{
    public Task<Result<EsQuery>> TranslateMainQueryAsync(ISearchQuery<ISearchResult<ISearchModel>> query) =>
        TranslateAsync(query.Query);

    /// <summary>Translates the post filters, or returns <c>null</c> when there are none.</summary>
    public async Task<Result<EsQuery?>> TranslatePostFilterAsync(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        if (query.PostFilterQuery == null || query.PostFilterQuery.Values.All(clauses => clauses.Count == 0))
        {
            return Result.Ok<EsQuery?>(null);
        }

        var filter = await TranslateAsync(query.PostFilterQuery.ToDictionary(
            group => group.Key,
            group => group.Value.Cast<ISearchSubQuery>().ToList())).ConfigureAwait(false);
        return filter.IsSuccess ? Result.Ok<EsQuery?>(filter.Value) : filter.ToResult<EsQuery?>();
    }

    private Task<Result<EsQuery>> TranslateAsync(IDictionary<Occurance, List<ISearchSubQuery>>? clauses)
    {
        if (clauses == null || clauses.Values.All(group => group.Count == 0))
        {
            return Task.FromResult(Result.Ok<EsQuery>(new MatchAllQuery()));
        }

        return ElasticSearchBoolQueryBuilder.BuildAsync(
            clauses.SelectMany(group => group.Value.Select(subQuery => (group.Key, subQuery))),
            subQueryTranslators);
    }
}
