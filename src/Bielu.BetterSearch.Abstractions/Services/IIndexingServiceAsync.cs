using FluentResults;

namespace Bielu.BetterSearch.Abstractions.Services;

/// <summary>
/// Indexes and removes documents, and reports what it did to subscribers.
/// </summary>
/// <remarks>
/// The three observable streams share one contract. An observer gets <see cref="IObserver{T}.OnNext"/> once for each
/// operation that succeeded, after the provider has done it: a document indexed, a document removed, an index emptied.
/// A rejected document or a failed operation sends nothing; the returned <see cref="Result"/> carries the reason.
/// <see cref="IObserver{T}.OnError"/> is never sent, and <see cref="IObserver{T}.OnCompleted"/> is sent once, when
/// the service is disposed, so a stream stays open for every operation the service performs. Subscribing after
/// disposal sends <see cref="IObserver{T}.OnCompleted"/> straight away, and operations after disposal notify nobody.
/// An observer that throws is logged and skipped: the operation still returns its own <see cref="Result"/>, and the
/// other observers are still notified. Subscribing an observer that is already subscribed notifies it once, until
/// every handle <c>Subscribe</c> returned for it is disposed.
/// <para>
/// Observers are called one at a time, and never after <see cref="IObserver{T}.OnCompleted"/>, only while the service
/// is not used concurrently. When one instance runs operations in parallel, or is disposed while an operation runs,
/// an observer can get overlapping <see cref="IObserver{T}.OnNext"/> calls, or one last
/// <see cref="IObserver{T}.OnNext"/> after <see cref="IObserver{T}.OnCompleted"/>, so it must be thread-safe.
/// </para>
/// <para>
/// The DI scope owns the service and disposes it, which completes the streams for every subscriber in that scope.
/// Do not dispose an injected service yourself: dispose the handle <c>Subscribe</c> returned to stop listening. Only
/// code that creates the service itself, outside DI, should dispose it.
/// </para>
/// </remarks>
public interface IIndexingServiceAsync : IObservable<SearchDocument>, IObservable<DeleteDocumentRequest>, IObservable<DeleteAllDocumentsRequest>, IDisposable
{
    Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default);
    Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default);
    Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes the documents with <paramref name="ids"/> from <paramref name="index"/> and returns how many were
    /// removed; see <see cref="IIndexingProviderAsync.RemoveDocumentsAsync"/>. On success, the
    /// <see cref="DeleteDocumentRequest"/> observers get one <see cref="IObserver{T}.OnNext"/> for each distinct id,
    /// removed or not there; when the <see cref="Result"/> fails they get nothing, even for documents already removed.
    /// </summary>
    Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default);
    Task<Result<int>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default);
}
