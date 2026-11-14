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
        if (query.PostFilterQuery != null && query.PostFilterQuery.Values.Any(clauses => clauses.Count > 0))
        {
            var postFilter = await TranslateAsync(query.PostFilterQuery.ToDictionary(
                group => group.Key,
                group => group.Value.Cast<ISearchSubQuery>().ToList())).ConfigureAwait(false);
            if (postFilter.IsFailed)
            {
                return postFilter.ToResult<TypesenseSearchRequest>();
            }

            filter = new TypesenseFilter.And([filter, postFilter.Value]);
            successes.AddRange(postFilter.Successes);
        }

        return Result.Ok(new TypesenseSearchRequest(filter)).WithSuccesses(successes);
    }

    private Task<Result<TypesenseFilter>> TranslateAsync(IDictionary<Occurance, List<ISearchSubQuery>>? clauses)
    {
        if (clauses == null || clauses.Values.All(group => group == null || group.Count == 0))
        {
            return Task.FromResult(Result.Ok(TypesenseFilter.MatchAll));
        }

        return TypesenseBoolFilterBuilder.BuildAsync(
            clauses.Where(group => group.Value != null).SelectMany(group => group.Value.Select(subQuery => (group.Key, subQuery))),
            subQueryTranslators);
    }
}
