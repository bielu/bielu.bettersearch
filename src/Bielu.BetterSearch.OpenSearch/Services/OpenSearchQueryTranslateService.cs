using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

/// <summary>
/// Translates the main query into one OpenSearch <c>bool</c> query, each sub-query a clause under
/// the occurrence it was added with. A query with no clauses matches every document.
/// </summary>
internal sealed class OpenSearchQueryTranslateService(IEnumerable<ISubQueryTranslator<QueryContainer>> subQueryTranslators)
    : IQueryTranslateServiceAsync<QueryContainer>, IPostFilterTranslateServiceAsync<QueryContainer>
{
    public Task<Result<QueryContainer>> TranslateMainQueryAsync(ISearchQuery query) =>
        TranslateAsync(query.Query);

    /// <inheritdoc />
    public async Task<Result<QueryContainer?>> TranslatePostFilterAsync(ISearchQuery query)
    {
        if (query.PostFilterQuery == null || query.PostFilterQuery.Values.All(clauses => clauses.Count == 0))
        {
            return Result.Ok<QueryContainer?>(null);
        }

        var filter = await TranslateAsync(query.PostFilterQuery.ToDictionary(
            group => group.Key,
            group => group.Value.Cast<ISearchSubQuery>().ToList())).ConfigureAwait(false);
        return filter.IsSuccess ? Result.Ok<QueryContainer?>(filter.Value) : filter.ToResult<QueryContainer?>();
    }

    private Task<Result<QueryContainer>> TranslateAsync(IDictionary<Occurance, List<ISearchSubQuery>>? clauses)
    {
        if (clauses == null || clauses.Values.All(group => group.Count == 0))
        {
            return Task.FromResult(Result.Ok<QueryContainer>(new MatchAllQuery()));
        }

        return OpenSearchBoolQueryBuilder.BuildAsync(
            clauses.SelectMany(group => group.Value.Select(subQuery => (group.Key, subQuery))),
            subQueryTranslators);
    }
}
