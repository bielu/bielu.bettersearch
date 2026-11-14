using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OsSortOrder = OpenSearch.Net.SortOrder;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchSearchProviderAsync(
    IQueryTranslateServiceAsync<QueryContainer> queryTranslateServiceAsync,
    IPostFilterTranslateServiceAsync<QueryContainer> postFilterTranslateService,
    IClientFactoryAsync<IOpenSearchClient> clientFactoryAsync,
    IResultMapper<ISearchResponse<SearchDocument>> resultMapper,
    IOptionsMonitor<OpenSearchOptions> optionsMonitor,
    IOpenSearchClientSettingsManager settingsManager) : ISearchProviderAsync
{
    /// <summary>Used when <c>PageSize</c> is not set, as OpenSearch does.</summary>
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
            return Result.Fail<ISearchResult>("The OpenSearch provider does not support facets yet.");
        }

        var maxResultWindow = optionsMonitor.CurrentValue.MaxResultWindow;
        if (maxResultWindow <= 0)
        {
            return Result.Fail<ISearchResult>(
                $"MaxResultWindow is {maxResultWindow}, but it must be greater than 0. Set {OpenSearchOptions.OpenSearchOptionsKey}:MaxResultWindow to the indexes' index.max_result_window.");
        }

        if (LastResultOf(query) > maxResultWindow)
        {
            return Result.Fail<ISearchResult>(
                $"Page {PageOf(query)} with PageSize {PageSizeOf(query)} reaches result {LastResultOf(query)}, past the limit of {maxResultWindow} (index.max_result_window). Narrow the query or raise MaxResultWindow with the index setting.");
        }

        var sortOrder = SortOrders.Ordered(query);
        if (sortOrder.IsFailed)
        {
            return sortOrder.ToResult<ISearchResult>();
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

        var client = await clientFactoryAsync.TryGetClientAsync(query.Index).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<ISearchResult>();
        }

        var mappings = settingsManager.GetConnectionSettings(query.Index).Mappings ?? [];
        var request = CreateRequest(query, mainQuery.Value, postFilter.Value, maxResultWindow, sortOrder.Value, mappings);
        request.Highlight = highlight.Value;
        var response = await client.Value.SearchAsync<SearchDocument>(request, cancellationToken).ConfigureAwait(false);
        // The client reports a cancelled request as an invalid response; the caller cancelled, so say that instead.
        cancellationToken.ThrowIfCancellationRequested();
        // OpenSearch.Client treats some error responses as valid, so a server error is a failure too.
        if (!response.IsValid || response.ServerError is not null)
        {
            return Result.Fail<ISearchResult>(OpenSearchFailures.Describe("search", response));
        }

        // Partial results are disallowed in the request; this guards against a cluster that returns them anyway.
        if (response.TimedOut || response.Shards.Failed > 0)
        {
            return Result.Fail<ISearchResult>(
                $"OpenSearch returned incomplete results: timed out {response.TimedOut}, {response.Shards.Failed} of {response.Shards.Total} shards failed.");
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
    /// all fields is <c>fields.*</c>. Fragments use OpenSearch's default size and count.
    /// </summary>
    /// <summary>Sends <c>HEAD /</c> with the client of <paramref name="index"/>, or of the shared settings when it is <c>null</c>.</summary>
    public Task<Result> PingAsync(string? index = null, CancellationToken cancellationToken = default) =>
        clientFactoryAsync.TryWithClientAsync(index ?? string.Empty, async client =>
        {
            var response = await client.PingAsync(ct: cancellationToken).ConfigureAwait(false);
            return response.IsValid ? Result.Ok() : Result.Fail(OpenSearchFailures.Describe("ping", response));
        }, cancellationToken);

    private async Task<Result<IHighlight?>> TranslateHighlightAsync(ISearchQuery query)
    {
        var requested = Highlights.Requested(query, OpenSearchFieldNames.Resolve);
        if (requested.IsFailed || requested.Value.Count == 0)
        {
            return requested.IsFailed ? requested.ToResult<IHighlight?>() : Result.Ok<IHighlight?>(null);
        }

        var fields = new Dictionary<Field, IHighlightField>();
        foreach (var (field, request) in requested.Value)
        {
            var ownQuery = Highlights.OwnQuery(request);
            if (ownQuery.IsFailed)
            {
                return ownQuery.ToResult<IHighlight?>();
            }

            var highlightField = new HighlightField();
            if (ownQuery.Value != null)
            {
                var translated = await queryTranslateServiceAsync.TranslateMainQueryAsync(new SearchQuery { Index = query.Index, Query = ownQuery.Value }).ConfigureAwait(false);
                if (translated.IsFailed)
                {
                    return translated.ToResult<IHighlight?>();
                }

                highlightField.HighlightQuery = translated.Value;
            }

            fields[field] = highlightField;
        }

        return Result.Ok<IHighlight?>(new Highlight
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

    private static SearchRequest<SearchDocument> CreateRequest(ISearchQuery query, QueryContainer mainQuery, QueryContainer? postFilter, int maxResultWindow,
        IReadOnlyList<ISortOrderField> sortOrder, IReadOnlyList<OpenSearchFieldMapping> mappings)
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

        if (sortOrder.Count > 0)
        {
            request.Sort = sortOrder
                .Select(sort => (ISort)new FieldSort
                {
                    // A field mapped as text sorts on its keyword sub-field.
                    Field = FieldMappings.SortField(OpenSearchFieldNames.Resolve(sort.FieldName), mappings),
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
}
