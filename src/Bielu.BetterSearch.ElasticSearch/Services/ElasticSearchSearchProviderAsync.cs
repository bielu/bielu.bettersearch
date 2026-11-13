using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using FluentResults;
using Microsoft.Extensions.Options;
using EsQuery = Elastic.Clients.Elasticsearch.QueryDsl.Query;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchSearchProviderAsync(
    IQueryTranslateServiceAsync<EsQuery> queryTranslateServiceAsync,
    IPostFilterTranslateServiceAsync<EsQuery> postFilterTranslateService,
    IClientFactoryAsync<ElasticsearchClient> clientFactoryAsync,
    IResultMapper<SearchResponse<SearchDocument>> resultMapper,
    IOptionsMonitor<ElasticSearchOptions> optionsMonitor,
    IElasticSearchClientSettingsManager settingsManager) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as Elasticsearch does.</summary>
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
            return Result.Fail<ISearchResult>("The Elasticsearch provider does not support facets yet.");
        }

        var maxResultWindow = optionsMonitor.CurrentValue.MaxResultWindow;
        if (LastResultOf(query) > maxResultWindow)
        {
            return Result.Fail<ISearchResult>(
                $"Page {PageOf(query)} with PageSize {PageSizeOf(query)} reaches result {LastResultOf(query)}, past the limit of {maxResultWindow} (index.max_result_window). Narrow the query or raise MaxResultWindow with the index setting.");
        }

        var mainQuery = await queryTranslateServiceAsync.TranslateMainQueryAsync(query).ConfigureAwait(false);
        if (mainQuery.IsFailed)
        {
            return mainQuery.ToResult<ISearchResult>();
        }

        var postFilter = await postFilterTranslateService.TranslatePostFilterAsync(query).ConfigureAwait(false);
        if (postFilter.IsFailed)
        {
            return postFilter.ToResult<ISearchResult>();
        }

        var highlight = await TranslateHighlightAsync(query).ConfigureAwait(false);
        if (highlight.IsFailed)
        {
            return highlight.ToResult<ISearchResult>();
        }

        var mappings = settingsManager.GetConnectionSettings(query.Index).Mappings ?? [];
        var request = CreateRequest(query, mainQuery.Value, postFilter.Value, maxResultWindow, mappings);
        request.Highlight = highlight.Value;
        var client = await clientFactoryAsync.GetOrCreateClientAsync(query.Index).ConfigureAwait(false);
        var response = await client.SearchAsync<SearchDocument>(request, cancellationToken).ConfigureAwait(false);
        // The client reports a cancelled request as an invalid response; the caller cancelled, so say that instead.
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsValidResponse)
        {
            return Result.Fail<ISearchResult>(ElasticSearchFailures.Describe("search", response));
        }

        // Partial results are disallowed in the request; this guards against a cluster that returns them anyway.
        if (response.TimedOut || response.Shards.Failed > 0)
        {
            return Result.Fail<ISearchResult>(
                $"Elasticsearch returned incomplete results: timed out {response.TimedOut}, {response.Shards.Failed} of {response.Shards.Total} shards failed.");
        }

        var result = resultMapper.MapResult(response);
        result.Pagination.Page = PageOf(query);
        result.Pagination.PageSize = PageSizeOf(query);
        result.Pagination.TotalPages = (int)Math.Ceiling(result.Pagination.TotalCount / (double)result.Pagination.PageSize);
        return Result.Ok(result);
    }

    /// <summary>
    /// The <c>highlight</c> section for the query's <c>HighlightQueries</c>, or <c>null</c> when it has none. Each
    /// field is highlighted where the main query matched it, or where its own highlight query does when it has one;
    /// all fields is <c>fields.*</c>. Fragments use Elasticsearch's default size and count.
    /// </summary>
    private async Task<Result<Highlight?>> TranslateHighlightAsync(ISearchQuery query)
    {
        var requested = Highlights.Requested(query, ElasticSearchFieldNames.Resolve);
        if (requested.IsFailed || requested.Value.Count == 0)
        {
            return requested.IsFailed ? requested.ToResult<Highlight?>() : Result.Ok<Highlight?>(null);
        }

        var fields = new Dictionary<Field, HighlightField>();
        foreach (var (field, request) in requested.Value)
        {
            var ownQuery = Highlights.OwnQuery(request);
            if (ownQuery.IsFailed)
            {
                return ownQuery.ToResult<Highlight?>();
            }

            var highlightField = new HighlightField();
            if (ownQuery.Value != null)
            {
                var translated = await queryTranslateServiceAsync.TranslateMainQueryAsync(new SearchQuery { Index = query.Index, Query = ownQuery.Value }).ConfigureAwait(false);
                if (translated.IsFailed)
                {
                    return translated.ToResult<Highlight?>();
                }

                highlightField.HighlightQuery = translated.Value;
            }

            fields[field] = highlightField;
        }

        return Result.Ok<Highlight?>(new Highlight
        {
            Fields = fields,
            // Escapes the document text, so only the tags are markup.
            Encoder = HighlighterEncoder.Html,
            PreTags = [SearchHit.HighlightPreTag],
            PostTags = [SearchHit.HighlightPostTag]
        });
    }

    /// <summary><c>Page</c> counts from 1; 0, the default, is the first page.</summary>
    private static int PageOf(ISearchQuery query) => Math.Max(query.Page, 1);

    private static int PageSizeOf(ISearchQuery query) =>
        query.PageSize > 0 ? query.PageSize : DefaultPageSize;

    /// <summary>The position of the page's last result, in <c>long</c> so a large page can't overflow.</summary>
    private static long LastResultOf(ISearchQuery query) => (long)PageOf(query) * PageSizeOf(query);

    private static SearchRequest CreateRequest(ISearchQuery query, EsQuery mainQuery, EsQuery? postFilter, int maxResultWindow,
        IReadOnlyList<ElasticSearchFieldMapping> mappings)
    {
        var pageSize = PageSizeOf(query);
        var request = new SearchRequest(query.Index)
        {
            Query = mainQuery,
            PostFilter = postFilter,
            From = (PageOf(query) - 1) * pageSize,
            Size = pageSize,
            AllowPartialSearchResults = false,
            // Counts exactly as far as a page can reach, so every page TotalPages names can be fetched.
            TrackTotalHits = new TrackHits(maxResultWindow)
        };

        if (query.SortOrder is { Count: > 0 })
        {
            request.Sort = query.SortOrder
                .OrderBy(sort => sort.Order)
                .Select(sort => new SortOptions
                {
                    Field = new FieldSort
                    {
                        // A field mapped as text sorts on its keyword sub-field.
                        Field = FieldMappings.SortField(ElasticSearchFieldNames.Resolve(sort.FieldName), mappings),
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
}
