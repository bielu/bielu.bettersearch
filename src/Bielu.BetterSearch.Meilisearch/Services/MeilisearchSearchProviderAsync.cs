using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Options;
using DocumentsQuery = Meilisearch.QueryParameters.DocumentsQuery;
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

    internal const string UnsupportedHighlighting =
        "The Meilisearch provider does not support highlighting: it runs every query as a filter with no search text, " +
        "and Meilisearch marks only matches of the search text, so there is nothing for it to highlight.";

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
            return Result.Fail<ISearchResult>(UnsupportedHighlighting);
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

    /// <summary>Gets the stored document. <c>null</c> when Meilisearch answers that the document or the index is not there.</summary>
    public async Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var check = DocumentLookups.CheckGet(index, id);
        if (check.IsFailed)
        {
            return check.ToResult<SearchDocument?>();
        }

        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var stored = await client.Index(index).GetDocumentAsync<MeilisearchDocument>(id, ["id", "type", MeilisearchFieldNames.Fields], cancellationToken).ConfigureAwait(false);
            return Result.Ok<SearchDocument?>(new SearchDocument
            {
                Id = stored.Id,
                Type = stored.Type ?? string.Empty,
                Index = index,
                Fields = DocumentLookups.ToFields(StoredFields.ToCustomProperties(stored.Fields))
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MeilisearchApiError e) when (e.Code is "index_not_found" or MeilisearchIndexingProviderAsync.DocumentNotFound)
        {
            return Result.Ok<SearchDocument?>(null);
        }
        catch (MeilisearchApiError e)
        {
            return Result.Fail<SearchDocument?>(
                new Error($"Meilisearch get of document {id} failed ({e.Code}): {e.Message}").WithMetadata("code", e.Code).CausedBy(e));
        }
        catch (Exception e)
        {
            return Result.Fail<SearchDocument?>(new Error($"Meilisearch get of document {id} failed: {e.Message}").CausedBy(e));
        }
    }

    /// <summary>
    /// Counts with a filtered document fetch rather than a search, so the count is exact and not capped at
    /// <c>maxTotalHits</c>. The filter is the one <see cref="SearchAsync"/> sends, post filter included.
    /// </summary>
    public async Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var check = DocumentLookups.CheckCount(query.Index);
        if (check.IsFailed)
        {
            return check.ToResult<long>();
        }

        var request = await queryTranslateService.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (request.IsFailed)
        {
            return request.ToResult<long>();
        }

        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
            var documents = await client.Index(query.Index).GetDocumentsAsync<MeilisearchDocument>(
                new DocumentsQuery { Filter = request.Value.Filter, Fields = ["id"], Limit = 1 }, cancellationToken).ConfigureAwait(false);
            return Result.Ok((long)documents.Total).WithSuccesses(request.Successes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MeilisearchApiError e)
        {
            return Result.Fail<long>(
                new Error($"Meilisearch count failed ({e.Code}): {e.Message}").WithMetadata("code", e.Code).CausedBy(e));
        }
        catch (Exception e)
        {
            return Result.Fail<long>(new Error($"Meilisearch count failed: {e.Message}").CausedBy(e));
        }
    }

    private static Result<IEnumerable<string>?> Sort(ISearchQuery query)
    {
        var sortOrder = SortOrders.Ordered(query);
        if (sortOrder.IsFailed || sortOrder.Value.Count == 0)
        {
            return sortOrder.IsFailed ? sortOrder.ToResult<IEnumerable<string>?>() : Result.Ok<IEnumerable<string>?>(null);
        }

        var sort = new List<string>();
        foreach (var field in sortOrder.Value)
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
