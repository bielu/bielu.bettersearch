using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Translates the main query and the post filters into one filter, both of which must match. Without facets, which
/// the provider does not support yet, a post filter and a filter return the same hits.
/// </summary>
internal sealed class TypesenseQueryTranslateService(IEnumerable<ISubQueryTranslator<TypesenseFilter>> subQueryTranslators)
    : IQueryTranslateServiceAsync<TypesenseSearchRequest>
{
    public async Task<Result<TypesenseSearchRequest>> TranslateMainQueryAsync(ISearchQuery query)
    {
        var main = await TranslateAsync(query.Query).ConfigureAwait(false);
        if (main.IsFailed)
        {
            return main.ToResult<TypesenseSearchRequest>();
        }

        var successes = main.Successes.ToList();
        var filter = main.Value;
        if (query.PostFilterQuery != null && query.PostFilterQuery.Count > 0)
        {
            var postFilter = await TranslateAsync(query.PostFilterQuery).ConfigureAwait(false);
            if (postFilter.IsFailed)
            {
                return postFilter.ToResult<TypesenseSearchRequest>();
            }

            filter = new TypesenseFilter.And([filter, postFilter.Value]);
            successes.AddRange(postFilter.Successes);
        }

        return Result.Ok(new TypesenseSearchRequest(filter)).WithSuccesses(successes);
    }

    private Task<Result<TypesenseFilter>> TranslateAsync(IList<ISearchSubQuery>? clauses)
    {
        if (clauses == null || clauses.Count == 0)
        {
            return Task.FromResult(Result.Ok(TypesenseFilter.MatchAll));
        }

        return TypesenseBoolFilterBuilder.BuildAsync(
            clauses,
            subQueryTranslators);
    }
}
