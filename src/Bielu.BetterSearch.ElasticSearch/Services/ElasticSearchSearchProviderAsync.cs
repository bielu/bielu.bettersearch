using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using FluentResults;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchSearchProviderAsync(
    IQueryTranslateServiceAsync<EsQuery> queryTranslateServiceAsync,
    ElasticSearchQueryTranslateService postFilterTranslateService,
    IClientFactoryAsync<ElasticsearchClient> clientFactoryAsync,
    IResultMapper<SearchResponse<SearchDocument>> resultMapper) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as Elasticsearch does.</summary>
    public const int DefaultPageSize = 10;

    public async Task<Result<ISearchResult<ISearchModel>>> SearchAsync(ISearchQuery<ISearchResult<ISearchModel>> query)
    {
        if (string.IsNullOrWhiteSpace(query.Index))
        {
            return Result.Fail<ISearchResult<ISearchModel>>("The query needs an Index.");
        }

        if (query.FacetQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult<ISearchModel>>("The Elasticsearch provider does not support facets yet.");
        }

        if (query.HighlightQueries is { Count: > 0 })
        {
            return Result.Fail<ISearchResult<ISearchModel>>("The Elasticsearch provider does not support highlighting yet.");
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

        var client = await clientFactoryAsync.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
        var response = await client.SearchAsync<SearchDocument>(CreateRequest(query, mainQuery.Value, postFilter.Value)).ConfigureAwait(false);
        if (!response.IsValidResponse)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(DescribeFailure(response));
        }

        // Partial results are disallowed in the request; this guards against a cluster that returns them anyway.
        if (response.TimedOut || response.Shards.Failed > 0)
        {
            return Result.Fail<ISearchResult<ISearchModel>>(
                $"Elasticsearch returned incomplete results: timed out {response.TimedOut}, {response.Shards.Failed} of {response.Shards.Total} shards failed.");
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

    private static SearchRequest CreateRequest(ISearchQuery<ISearchResult<ISearchModel>> query, EsQuery mainQuery, EsQuery? postFilter)
    {
        var pageSize = PageSizeOf(query);
        var request = new SearchRequest(query.Index)
        {
            Query = mainQuery,
            PostFilter = postFilter,
            From = (PageOf(query) - 1) * pageSize,
            Size = pageSize,
            TrackTotalHits = new TrackHits(true),
            AllowPartialSearchResults = false
        };

        if (query.SortOrder is { Count: > 0 })
        {
            request.Sort = query.SortOrder
                .OrderBy(sort => sort.Order)
                .Select(sort => new SortOptions
                {
                    Field = new FieldSort
                    {
                        Field = ElasticSearchFieldNames.Resolve(sort.FieldName),
                        Order = sort.Desc ? SortOrder.Desc : SortOrder.Asc
                    }
                })
                .ToList();
        }

        var properties = query.Properties?.Where(property => !string.IsNullOrWhiteSpace(property)).ToList();
        if (properties is { Count: > 0 })
        {
            string[] metadata = [nameof(SearchDocument.Id), nameof(SearchDocument.Type), nameof(SearchDocument.Index)];
            request.Source = new SourceConfig(new SourceFilter
            {
                Includes = Fields.FromStrings(metadata
                    .Select(name => name.ToLowerInvariant())
                    .Concat(properties.Select(ElasticSearchFieldNames.Resolve))
                    .ToArray())
            });
        }

        return request;
    }

    private static string DescribeFailure(SearchResponse<SearchDocument> response)
    {
        var error = response.ElasticsearchServerError?.Error;
        if (error == null)
        {
            return $"Elasticsearch search failed: {response.ApiCallDetails.OriginalException?.Message ?? response.DebugInformation}";
        }

        var cause = error.RootCause?.FirstOrDefault(rootCause => rootCause.Reason != error.Reason)?.Reason;
        return cause == null
            ? $"Elasticsearch search failed ({error.Type}): {error.Reason}"
            : $"Elasticsearch search failed ({error.Type}): {error.Reason}: {cause}";
    }
}
