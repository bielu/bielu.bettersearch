using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Services;

/// <summary>The default <see cref="ISearchServiceAsync"/>; see it for the observer contract.</summary>
public class SearchServiceAsync(ISearchProviderAsync providerAsync, ILogger<SearchServiceAsync>? logger = null)
    : ISearchServiceAsync
{
    private readonly ObserverList<ISearchModel> _searchObservers = new(logger);

    public IDisposable Subscribe(IObserver<ISearchModel> observer) => _searchObservers.Subscribe(observer);

    public async Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await providerAsync.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailed)
        {
            return result;
        }

        foreach (var hit in result.Value.Items)
        {
            _searchObservers.Notify(hit.Document);
        }

        return result;
    }

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
