using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using FluentResults;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OsSortOrder = OpenSearch.Net.SortOrder;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

public sealed class OpenSearchSearchProviderAsync(
    IQueryTranslateServiceAsync<QueryContainer> queryTranslateServiceAsync,
    IPostFilterTranslateServiceAsync<QueryContainer> postFilterTranslateService,
    IClientFactoryAsync<IOpenSearchClient> clientFactoryAsync,
    IResultMapper<ISearchResponse<SearchDocument>> resultMapper,
    IOptionsMonitor<OpenSearchOptions> optionsMonitor) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as OpenSearch does.</summary>
    public const int DefaultPageSize = 10;

    public async Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        if (string.IsNullOrWhiteSpace(query.Index))
        {
            return Result.Fail<ISearchResult<ISearchModel>>("The query needs an Index.");
        }

        if (query.FacetQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult<ISearchModel>>("The OpenSearch provider does not support facets yet.");
        }

        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult<ISearchModel>>("The OpenSearch provider does not support highlighting yet.");
        }

        var maxResultWindow = optionsMonitor.CurrentValue.MaxResultWindow;
        if (maxResultWindow <= 0)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(
                $"MaxResultWindow is {maxResultWindow}, but it must be greater than 0. Set {OpenSearchOptions.OpenSearchOptionsKey}:MaxResultWindow to the indexes' index.max_result_window.");
        }

        if (LastResultOf(query) > maxResultWindow)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(
                $"Page {PageOf(query)} with PageSize {PageSizeOf(query)} reaches result {LastResultOf(query)}, past the limit of {maxResultWindow} (index.max_result_window). Narrow the query or raise MaxResultWindow with the index setting.");
        }

        var mainQuery = await queryTranslateServiceAsync.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (mainQuery.IsFailed)
        {
            return mainQuery.ToResult<ISearchResult<ISearchModel>>();
        }

        var postFilter = await postFilterTranslateService.TranslatePostFilterAsync(query).ConfigureAwait(false);
        if (postFilter.IsFailed)
        {
            return postFilter.ToResult<ISearchResult<ISearchModel>>();
        }

        var client = await clientFactoryAsync.TryGetClientAsync(query.Index).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<ISearchResult<ISearchModel>>();
        }

        var response = await client.Value.SearchAsync<SearchDocument>(CreateRequest(query, mainQuery.Value, postFilter.Value, maxResultWindow)).ConfigureAwait(false);
        // OpenSearch.Client treats some error responses as valid, so a server error is a failure too.
        if (!response.IsValid || response.ServerError is not null)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(DescribeFailure(response));
        }

        // Partial results are disallowed in the request; this guards against a cluster that returns them anyway.
        if (response.TimedOut || response.Shards.Failed > 0)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(
                $"OpenSearch returned incomplete results: timed out {response.TimedOut}, {response.Shards.Failed} of {response.Shards.Total} shards failed.");
        }

        var result = resultMapper.MapResult(response);
        result.Pagination.Page = PageOf(query);
        result.Pagination.PageSize = PageSizeOf(query);
        result.Pagination.TotalPages = (int)Math.Ceiling(result.Pagination.TotalCount / (double)result.Pagination.PageSize);
        result.Pagination.TotalNumberOfPages = result.Pagination.TotalPages;
        return Result.Ok(result);
    }

    /// <summary><c>Page</c> counts from 1; 0, the default, is the first page.</summary>
    private static int PageOf(ISearchQuery<ISearchResult<ISearchModel>> query) => Math.Max(query.Page, 1);

    private static int PageSizeOf(ISearchQuery<ISearchResult<ISearchModel>> query) =>
        query.PageSize > 0 ? query.PageSize : DefaultPageSize;

    /// <summary>The position of the page's last result, in <c>long</c> so a large page can't overflow.</summary>
    private static long LastResultOf(ISearchQuery<ISearchResult<ISearchModel>> query) => (long)PageOf(query) * PageSizeOf(query);

    private static SearchRequest<SearchDocument> CreateRequest(ISearchQuery<ISearchResult<ISearchModel>> query, QueryContainer mainQuery, QueryContainer? postFilter, int maxResultWindow)
    {
        var pageSize = PageSizeOf(query);
        var request = new SearchRequest<SearchDocument>(query.Index)
        {
            Query = mainQuery,
            PostFilter = postFilter,
            From = (PageOf(query) - 1) * pageSize,
            Size = pageSize,
            AllowPartialSearchResults = false,
            // Counts exactly as far as a page can reach, so every page TotalPages names can be fetched.
            TrackTotalHits = new TrackTotalHits(maxResultWindow)
        };

        if (query.SortOrder is { Count: > 0 })
        {
            request.Sort = query.SortOrder
                .OrderBy(sort => sort.Order)
                .Select(sort => (ISort)new FieldSort
                {
                    Field = OpenSearchFieldNames.Resolve(sort.FieldName),
                    Order = sort.Desc ? OsSortOrder.Descending : OsSortOrder.Ascending
                })
                .ToList();
        }

        var properties = query.Properties?.Where(property => !string.IsNullOrWhiteSpace(property)).ToList();
        if (properties is { Count: > 0 })
        {
            string[] metadata = [nameof(SearchDocument.Id), nameof(SearchDocument.Type), nameof(SearchDocument.Index)];
            request.Source = new Union<bool, ISourceFilter>(new SourceFilter
            {
                Includes = metadata
                    .Select(name => name.ToLowerInvariant())
                    .Concat(properties.Select(OpenSearchFieldNames.Resolve))
                    .ToArray()
            });
        }

        return request;
    }

    private static string DescribeFailure(ISearchResponse<SearchDocument> response)
    {
        var error = response.ServerError?.Error;
        if (error == null)
        {
            return $"OpenSearch search failed: {response.OriginalException?.Message ?? response.DebugInformation}";
        }

        var cause = error.RootCause?.FirstOrDefault(rootCause => rootCause.Reason != error.Reason)?.Reason;
        return cause == null
            ? $"OpenSearch search failed ({error.Type}): {error.Reason}"
            : $"OpenSearch search failed ({error.Type}): {error.Reason}: {cause}";
    }
}
