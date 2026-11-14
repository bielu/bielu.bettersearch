using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace Bielu.BetterSearch.Services;

/// <summary>
/// The unkeyed <see cref="IIndexingServiceAsync"/> when indexes are routed: runs each write on the keyed indexing
/// service that <see cref="IndexRoutes"/> picks for its index. Its observers hear everything that keyed service
/// reports for a write run through this service.
/// </summary>
internal sealed class RoutingIndexingServiceAsync : IIndexingServiceAsync
{
    private readonly IndexRoutes _routes;
    private readonly ObserverList<SearchDocument> _indexingObservers;
    private readonly ObserverList<DeleteDocumentRequest> _deleteDocumentObservers;
    private readonly ObserverList<DeleteAllDocumentsRequest> _deleteAllDocumentsObservers;
    private readonly RoutedServices<IIndexingServiceAsync, RoutedWrite> _services;

    public RoutingIndexingServiceAsync(IServiceProvider serviceProvider, IndexRoutes routes, ILogger<RoutingIndexingServiceAsync>? logger = null)
    {
        _routes = routes;
        _indexingObservers = new ObserverList<SearchDocument>(logger);
        _deleteDocumentObservers = new ObserverList<DeleteDocumentRequest>(logger);
        _deleteAllDocumentsObservers = new ObserverList<DeleteAllDocumentsRequest>(logger);
        _services = new RoutedServices<IIndexingServiceAsync, RoutedWrite>(serviceProvider, (service, current) =>
        [
            service.Subscribe(new ForwardingObserver<SearchDocument>(document =>
            {
                if (current() is { } write)
                {
                    write.Indexed(document);
                    _indexingObservers.Notify(document);
                }
            })),
            service.Subscribe(new ForwardingObserver<DeleteDocumentRequest>(request =>
            {
                if (current() is not null)
                {
                    _deleteDocumentObservers.Notify(request);
                }
            })),
            service.Subscribe(new ForwardingObserver<DeleteAllDocumentsRequest>(request =>
            {
                if (current() is not null)
                {
                    _deleteAllDocumentsObservers.Notify(request);
                }
            })),
        ]);
    }

    public IDisposable Subscribe(IObserver<SearchDocument> observer) => _indexingObservers.Subscribe(observer);

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer) => _deleteDocumentObservers.Subscribe(observer);

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer) => _deleteAllDocumentsObservers.Subscribe(observer);

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var nullFields = IndexingServiceAsync.CheckNullFields(document);
        if (nullFields.IsFailed)
        {
            return nullFields;
        }

        var key = _routes.KeyFor(document.Index);
        if (key.IsFailed)
        {
            return key.ToResult();
        }

        return await _services.RunAsync(key.Value, new RoutedWrite(), service => service.IndexDocumentAsync(document, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Splits the documents by provider and calls each provider's indexing service once, in the order its first
    /// document appears. Nothing is indexed when a document is null, has a null field, or has an index with no route.
    /// When a provider fails, the others are still called, and the failed <see cref="Result"/> has one error saying
    /// what each provider stored; its reasons have one error for each provider, whose <c>key</c> metadata is the
    /// provider key and whose <c>ids</c> metadata holds the ids of the documents that provider stored.
    /// </summary>
    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default)
    {
        if (document is null)
        {
            return Result.Fail<int>("The documents are null; pass an empty list to index nothing.");
        }

        var documents = document.ToList();
        var nullFields = Result.Merge(documents.Select(IndexingServiceAsync.CheckNullFields).ToArray());
        if (nullFields.IsFailed)
        {
            return Result.Fail<int>(nullFields.Errors);
        }

        var groups = new List<(string Key, List<SearchDocument> Documents)>();
        foreach (var doc in documents)
        {
            var key = _routes.KeyFor(doc.Index);
            if (key.IsFailed)
            {
                return key.ToResult<int>();
            }

            var group = groups.FindIndex(g => g.Key == key.Value);
            if (group < 0)
            {
                groups.Add((key.Value, [doc]));
            }
            else
            {
                groups[group].Documents.Add(doc);
            }
        }

        var outcomes = new List<(string Key, int Requested, IReadOnlyList<string> StoredIds, Result<int> Result)>();
        foreach (var (key, keyDocuments) in groups)
        {
            var write = new RoutedWrite();
            var result = await _services.RunAsync(key, write, service => service.IndexMultipleDocumentsAsync(keyDocuments, cancellationToken)).ConfigureAwait(false);
            outcomes.Add((key, keyDocuments.Count, write.IndexedIds(), result));
        }

        if (outcomes.All(outcome => outcome.Result.IsSuccess))
        {
            return Result.Ok(outcomes.Sum(outcome => outcome.Result.Value));
        }

        var reasons = outcomes.Select(outcome =>
        {
            var stored = $"'{outcome.Key}' stored {outcome.StoredIds.Count} of {outcome.Requested} documents";
            var error = outcome.Result.IsSuccess
                ? new Error(stored)
                : new Error($"{stored} and failed: {string.Join("; ", outcome.Result.Errors.Select(e => e.Message))}").CausedBy(outcome.Result.Errors);
            return error.WithMetadata("key", outcome.Key).WithMetadata("ids", outcome.StoredIds.ToList());
        }).ToList();
        var failed = outcomes.Count(outcome => outcome.Result.IsFailed);
        return Result.Fail<int>(new Error($"Indexing the documents failed on {failed} of {outcomes.Count} providers: {string.Join("; ", reasons.Select(r => r.Message))}.")
            .WithMetadata("ids", outcomes.SelectMany(outcome => outcome.StoredIds).ToList())
            .CausedBy(reasons));
    }

    /// <summary>Routes by <see cref="DeleteDocumentRequest.Type"/>, which holds the index name.</summary>
    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default)
    {
        var key = _routes.KeyFor(document?.Type);
        if (key.IsFailed)
        {
            return key.ToResult();
        }

        return await _services.RunAsync(key.Value, new RoutedWrite(), service => service.RemoveDocumentAsync(document!, cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var key = _routes.KeyFor(index);
        if (key.IsFailed)
        {
            return key.ToResult<int>();
        }

        return await _services.RunAsync(key.Value, new RoutedWrite(), service => service.RemoveDocumentsAsync(index, ids, cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        var key = _routes.KeyFor(request?.IndexName);
        if (key.IsFailed)
        {
            return key.ToResult<long?>();
        }

        return await _services.RunAsync(key.Value, new RoutedWrite(), service => service.RemoveAllDocumentsAsync(request!, cancellationToken)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _services.Dispose();
        _indexingObservers.Complete();
        _deleteDocumentObservers.Complete();
        _deleteAllDocumentsObservers.Complete();
    }

    /// <summary>One routed write, recording the documents its keyed service reported as indexed.</summary>
    private sealed class RoutedWrite
    {
        private readonly List<string> _indexedIds = new();

        public void Indexed(SearchDocument document)
        {
            lock (_indexedIds)
            {
                _indexedIds.Add(document.Id);
            }
        }

        public IReadOnlyList<string> IndexedIds()
        {
            lock (_indexedIds)
            {
                return _indexedIds.ToList();
            }
        }
    }
}
