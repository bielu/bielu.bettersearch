using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;

namespace Bielu.BetterSearch.ElasticSearch.Services;

/// <summary>
/// Translates the main query into one Elasticsearch <c>bool</c> query, each sub-query a clause under
/// its own occurrence. A query with no clauses matches every document.
/// </summary>
internal sealed class ElasticSearchQueryTranslateService(IEnumerable<ISubQueryTranslator<EsQuery>> subQueryTranslators)
    : IQueryTranslateServiceAsync<EsQuery>, IPostFilterTranslateServiceAsync<EsQuery>
{
    public Task<Result<EsQuery>> TranslateMainQueryAsync(ISearchQuery query) =>
        TranslateAsync(query.Query);

    /// <inheritdoc />
    public async Task<Result<EsQuery?>> TranslatePostFilterAsync(ISearchQuery query)
    {
        if (query.PostFilterQuery == null || query.PostFilterQuery.Count == 0)
        {
            return Result.Ok<EsQuery?>(null);
        }

        var filter = await TranslateAsync(query.PostFilterQuery).ConfigureAwait(false);
        return filter.IsSuccess ? Result.Ok<EsQuery?>(filter.Value) : filter.ToResult<EsQuery?>();
    }

    private Task<Result<EsQuery>> TranslateAsync(IList<ISearchSubQuery>? clauses)
    {
        if (clauses == null || clauses.Count == 0)
        {
            return Task.FromResult(Result.Ok<EsQuery>(new MatchAllQuery()));
        }

        return ElasticSearchBoolQueryBuilder.BuildAsync(
            clauses.Select(subQuery => (subQuery?.Occurrence ?? Occurrence.Must, subQuery!)),
            subQueryTranslators);
    }
}
