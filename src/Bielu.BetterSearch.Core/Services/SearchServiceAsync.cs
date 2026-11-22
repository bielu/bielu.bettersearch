using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Diagnostics;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Services;

/// <summary>The default <see cref="ISearchServiceAsync"/>.</summary>
public class SearchServiceAsync(
    ISearchProviderAsync providerAsync,
    ILogger<SearchServiceAsync>? logger = null,
    IOptionsMonitor<BetterSearchInstrumentationOptions>? instrumentationOptions = null)
    : ISearchServiceAsync
{
    /// <summary>Runs the search inside a <c>search</c> span; see <see cref="BetterSearchInstrumentation"/>.</summary>
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        BetterSearchTelemetry.RunAsync(
            "search",
            query?.Index,
            providerAsync,
            logger,
            BetterSearchTelemetry.RecordsErrorMessages(instrumentationOptions),
            activity =>
            {
                activity.SetTag(BetterSearchTelemetry.PageTag, query?.Page);
                activity.SetTag(BetterSearchTelemetry.PageSizeTag, query?.PageSize);
            },
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return providerAsync.SearchAsync(query!, cancellationToken);
            },
            (activity, result) =>
            {
                activity.SetTag(BetterSearchTelemetry.HitCountTag, result.Value.Items.Count);
                activity.SetTag(BetterSearchTelemetry.TotalCountTag, result.Value.Pagination?.TotalCount);
            });

    /// <summary>Gets the document inside a <c>get</c> span, whose hit count is 1 when it was found and 0 when not.</summary>
    public Task<Result<SearchDocument?>> GetDocumentAsync(string index, string id, CancellationToken cancellationToken = default) =>
        BetterSearchTelemetry.RunAsync(
            "get",
            index,
            providerAsync,
            logger,
            BetterSearchTelemetry.RecordsErrorMessages(instrumentationOptions),
            null,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var check = DocumentLookups.CheckGet(index, id);
                return check.IsFailed
                    ? Task.FromResult(check.ToResult<SearchDocument?>())
                    : providerAsync.GetDocumentAsync(index, id, cancellationToken);
            },
            (activity, result) => activity.SetTag(BetterSearchTelemetry.HitCountTag, result.Value is null ? 0 : 1));

    /// <summary>Counts inside a <c>count</c> span, whose total count is the count.</summary>
    public Task<Result<long>> CountAsync(ISearchQuery query, CancellationToken cancellationToken = default) =>
        BetterSearchTelemetry.RunAsync(
            "count",
            query?.Index,
            providerAsync,
            logger,
            BetterSearchTelemetry.RecordsErrorMessages(instrumentationOptions),
            null,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var check = DocumentLookups.CheckCount(query?.Index);
                return check.IsFailed
                    ? Task.FromResult(check.ToResult<long>())
                    : providerAsync.CountAsync(query!, cancellationToken);
            },
            (activity, result) => activity.SetTag(BetterSearchTelemetry.TotalCountTag, result.Value));
}
