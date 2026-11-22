using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.PostgreSql.Services;

/// <summary>
/// Translates the main query and the post filters into one filter, both of which must match. Without facets, which
/// the provider does not support yet, a post filter and a filter return the same hits.
/// </summary>
internal sealed class PostgreSqlQueryTranslateService(IEnumerable<ISubQueryTranslator<PostgreSqlFilter>> subQueryTranslators)
    : IQueryTranslateServiceAsync<PostgreSqlSearchRequest>
{
    public async Task<Result<PostgreSqlSearchRequest>> TranslateMainQueryAsync(ISearchQuery query)
    {
        var filter = await TranslateFilterAsync(query).ConfigureAwait(false);
        return filter.IsFailed
            ? filter.ToResult<PostgreSqlSearchRequest>()
            : Result.Ok(new PostgreSqlSearchRequest(filter.Value)).WithSuccesses(filter.Successes);
    }

    private async Task<Result<PostgreSqlFilter>> TranslateFilterAsync(ISearchQuery query)
    {
        var main = await TranslateAsync(query.Query).ConfigureAwait(false);
        if (main.IsFailed || query.PostFilterQuery == null || query.PostFilterQuery.Count == 0)
        {
            return main;
        }

        var postFilter = await TranslateAsync(query.PostFilterQuery).ConfigureAwait(false);
        if (postFilter.IsFailed)
        {
            return postFilter;
        }

        return Result.Ok<PostgreSqlFilter>(new PostgreSqlFilter.And([main.Value, postFilter.Value]))
            .WithSuccesses(main.Successes).WithSuccesses(postFilter.Successes);
    }

    private Task<Result<PostgreSqlFilter>> TranslateAsync(IList<ISearchSubQuery>? clauses)
    {
        if (clauses == null || clauses.Count == 0)
        {
            return Task.FromResult(Result.Ok(PostgreSqlFilter.MatchAll));
        }

        return PostgreSqlBoolFilterBuilder.BuildAsync(
            clauses,
            subQueryTranslators);
    }
}
