using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Options;
using MeilisearchApiError = Meilisearch.MeilisearchApiError;
using MeilisearchClient = Meilisearch.MeilisearchClient;
using MeilisearchQuery = Meilisearch.SearchQuery;
using PaginatedSearchResult = Meilisearch.PaginatedSearchResult<Bielu.BetterSearch.Meilisearch.Services.MeilisearchDocument>;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Meilisearch.Services;

/// <summary>
/// Runs the query as a Meilisearch filter with no search text, so every hit matches it exactly and has the
/// same ranking score. Hits are ordered by <c>SortOrder</c> when given.
/// </summary>
public sealed class MeilisearchSearchProviderAsync(
    IQueryTranslateServiceAsync<MeilisearchQuery> queryTranslateService,
    IClientFactoryAsync<MeilisearchClient> clientFactory,
    IOptionsMonitor<MeilisearchOptions> optionsMonitor) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as for Elasticsearch.</summary>
    public const int DefaultPageSize = 10;

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query.Index))
        {
            return Result.Fail<ISearchResult>("The query needs an Index.");
        }

        if (query.FacetQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>("The Meilisearch provider does not support facets yet.");
        }

        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult>("The Meilisearch provider does not support highlighting yet.");
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize > 0 ? query.PageSize : DefaultPageSize;
        var maxTotalHits = optionsMonitor.CurrentValue.MaxTotalHits;
        if ((long)page * pageSize > maxTotalHits)
        {
            return Result.Fail<ISearchResult>(
                $"Page {page} with PageSize {pageSize} reaches result {(long)page * pageSize}, past the limit of {maxTotalHits} (MeilisearchOptions.MaxTotalHits).");
        }

        var request = await queryTranslateService.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (request.IsFailed)
        {
            return request.ToResult<ISearchResult>();
        }

        var sort = Sort(query);
        if (sort.IsFailed)
        {
            return sort.ToResult<ISearchResult>();
        }

        var properties = query.Properties?.Where(property => !string.IsNullOrWhiteSpace(property)).ToList();
        var meilisearchQuery = request.Value;
        meilisearchQuery.Page = page;
        meilisearchQuery.HitsPerPage = pageSize;
        meilisearchQuery.Sort = sort.Value;
        meilisearchQuery.ShowRankingScore = true;
        // Only what results carry, not the copies the filters use.
        meilisearchQuery.AttributesToRetrieve = properties is { Count: > 0 }
            ? ["id", "type", .. properties.Select(property => MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, property))]
            : ["id", "type", MeilisearchFieldNames.Fields];

        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
            var response = await client.Index(query.Index).SearchAsync<MeilisearchDocument>(null, meilisearchQuery, cancellationToken).ConfigureAwait(false);
            if (response is not PaginatedSearchResult paginated)
            {
                return Result.Fail<ISearchResult>("Meilisearch returned an unpaginated result for a paginated search.");
            }

            var totalPages = (int)Math.Ceiling(paginated.TotalHits / (double)pageSize);
            return Result.Ok<ISearchResult>(new BaseSearchResults
            {
                Items = paginated.Hits.Select(MapHit).ToList(),
                Pagination = new Pagination
                {
                    TotalCount = paginated.TotalHits,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                }
            }).WithSuccesses(request.Successes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MeilisearchApiError e)
        {
            return Result.Fail<ISearchResult>(
                new Error($"Meilisearch search failed ({e.Code}): {e.Message}").WithMetadata("code", e.Code).CausedBy(e));
        }
        catch (Exception e)
        {
            return Result.Fail<ISearchResult>(new Error($"Meilisearch search failed: {e.Message}").CausedBy(e));
        }
    }

    private static Result<IEnumerable<string>?> Sort(ISearchQuery query)
    {
        if (query.SortOrder is not { Count: > 0 })
        {
            return Result.Ok<IEnumerable<string>?>(null);
        }

        var sort = new List<string>();
        foreach (var field in query.SortOrder.OrderBy(field => field.Order))
        {
            var name = MeilisearchFieldNames.Require(field.FieldName, "SortOrder");
            if (name.IsFailed)
            {
                return name.ToResult<IEnumerable<string>?>();
            }

            sort.Add($"{MeilisearchFieldNames.In(MeilisearchFieldNames.Fields, name.Value)}:{(field.Desc ? "desc" : "asc")}");
        }

        return Result.Ok<IEnumerable<string>?>(sort);
    }

    private static ISearchHit MapHit(MeilisearchDocument hit) => new SearchHit(new BaseSearchModel
    {
        Id = hit.Id,
        ContentType = hit.Type ?? string.Empty,
        CustomProperties = StoredFields.ToCustomProperties(hit.Fields)
    }, hit.RankingScore);
}
