using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using MeilisearchQuery = Meilisearch.SearchQuery;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Translates the main query and the post filters into one Meilisearch filter, both of which must match.
/// Without facets, which the provider does not support yet, a post filter and a filter return the same hits.
/// </summary>
internal sealed class MeilisearchQueryTranslateService(IEnumerable<ISubQueryTranslator<MeilisearchFilter>> subQueryTranslators)
    : IQueryTranslateServiceAsync<MeilisearchQuery>
{
    public async Task<Result<MeilisearchQuery>> TranslateMainQueryAsync(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        var main = await TranslateAsync(query.Query).ConfigureAwait(false);
        if (main.IsFailed)
        {
            return main.ToResult<MeilisearchQuery>();
        }

        var filters = new List<string> { main.Value.Expression };
        if (query.PostFilterQuery != null && query.PostFilterQuery.Values.Any(clauses => clauses.Count > 0))
        {
            var postFilter = await TranslateAsync(query.PostFilterQuery.ToDictionary(
                group => group.Key,
                group => group.Value.Cast<ISearchSubQuery>().ToList())).ConfigureAwait(false);
            if (postFilter.IsFailed)
            {
                return postFilter.ToResult<MeilisearchQuery>();
            }

            filters.Add(postFilter.Value.Expression);
        }

        return Result.Ok(new MeilisearchQuery
        {
            Filter = filters.Count == 1 ? filters[0] : string.Join(" AND ", filters.Select(filter => $"({filter})"))
        });
    }

    private Task<Result<MeilisearchFilter>> TranslateAsync(IDictionary<Occurance, List<ISearchSubQuery>>? clauses)
    {
        if (clauses == null || clauses.Values.All(group => group.Count == 0))
        {
            return Task.FromResult(Result.Ok(new MeilisearchFilter(MeilisearchFieldNames.MatchAll)));
        }

        return MeilisearchBoolFilterBuilder.BuildAsync(
            clauses.SelectMany(group => group.Value.Select(subQuery => (group.Key, subQuery))),
            subQueryTranslators);
    }
}
