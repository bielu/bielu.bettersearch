using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Diagnostics;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.BetterSearch.Services;

/// <summary>The default <see cref="ISearchServiceAsync"/>; see it for the observer contract.</summary>
public class SearchServiceAsync(
    ISearchProviderAsync providerAsync,
    ILogger<SearchServiceAsync>? logger = null,
    IOptionsMonitor<BetterSearchInstrumentationOptions>? instrumentationOptions = null)
    : ISearchServiceAsync
{
    private readonly ObserverList<ISearchModel> _searchObservers = new(logger);

    public IDisposable Subscribe(IObserver<ISearchModel> observer) => _searchObservers.Subscribe(observer);

    /// <summary>Runs the search inside a <c>search</c> span; see <see cref="BetterSearchInstrumentation"/>.</summary>
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        // Counted while the observers are notified, so the hits are enumerated once whether or not a span records them.
        var hitCount = 0;
        return BetterSearchTelemetry.RunAsync(
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
            () => SearchCoreAsync(query!, () => hitCount++, cancellationToken),
            (activity, result) =>
            {
                activity.SetTag(BetterSearchTelemetry.HitCountTag, hitCount);
                activity.SetTag(BetterSearchTelemetry.TotalCountTag, result.Value.Pagination?.TotalCount);
            });
    }

    private async Task<Result<ISearchResult>> SearchCoreAsync(ISearchQuery query, Action onHit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await providerAsync.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailed)
        {
            return result;
        }

        foreach (var hit in result.Value.Items)
        {
            onHit();
            _searchObservers.Notify(hit.Document);
        }

        return result;
    }

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

    /// <summary>Ends the stream: each observer gets <see cref="IObserver{T}.OnCompleted"/> once and is unsubscribed.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Ends the stream when <paramref name="disposing"/> is <c>true</c>. Never throws, and later calls do nothing.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchObservers.Complete();
        }
    }
}
