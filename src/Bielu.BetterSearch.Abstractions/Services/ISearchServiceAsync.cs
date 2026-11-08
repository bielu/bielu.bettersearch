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
/// <para>
/// Observers are called one at a time, and never after <see cref="IObserver{T}.OnCompleted"/>, only while the service
/// is not used concurrently. When one instance runs searches in parallel, or is disposed while a search runs, an
/// observer can get overlapping <see cref="IObserver{T}.OnNext"/> calls, or one last
/// <see cref="IObserver{T}.OnNext"/> after <see cref="IObserver{T}.OnCompleted"/>, so it must be thread-safe.
/// </para>
/// <para>
/// The DI scope owns the service and disposes it, which completes the stream for every subscriber in that scope.
/// Do not dispose an injected service yourself: dispose the handle <c>Subscribe</c> returned to stop listening. Only
/// code that creates the service itself, outside DI, should dispose it.
/// </para>
/// </remarks>
public interface ISearchServiceAsync : IObservable<ISearchModel>, IDisposable
{
    public Task<Result<ISearchResult>> SearchAsync(ISearchQuery query, CancellationToken cancellationToken = default);
}
