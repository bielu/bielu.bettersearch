using Bielu.BetterSearch.Abstractions.Models;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>
/// Searches, and reports every document it found to subscribers.
/// </summary>
/// <remarks>
/// Follows the same contract as <see cref="IIndexingServiceAsync"/>: <see cref="IObserver{T}.OnNext"/> once for each
/// hit of a successful search, nothing for a failed one, <see cref="IObserver{T}.OnError"/> never, and
/// <see cref="IObserver{T}.OnCompleted"/> once, when the service is disposed. An observer that throws is logged and
/// skipped, so the search still returns its result and the other observers are still notified.
/// </remarks>
public interface ISearchServiceAsync : IObservable<ISearchModel>, IDisposable
{
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default);
}
