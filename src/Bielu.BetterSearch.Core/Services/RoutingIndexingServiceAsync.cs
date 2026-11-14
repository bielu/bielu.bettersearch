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
    private readonly RoutedServices<IIndexingServiceAsync> _services;

    public RoutingIndexingServiceAsync(IServiceProvider serviceProvider, IndexRoutes routes, ILogger<RoutingIndexingServiceAsync>? logger = null)
    {
        _routes = routes;
        _indexingObservers = new ObserverList<SearchDocument>(logger);
        _deleteDocumentObservers = new ObserverList<DeleteDocumentRequest>(logger);
        _deleteAllDocumentsObservers = new ObserverList<DeleteAllDocumentsRequest>(logger);
        _services = new RoutedServices<IIndexingServiceAsync>(serviceProvider, (service, routed) =>
        [
            service.Subscribe(new ForwardingObserver<SearchDocument>(document =>
            {
                if (routed())
                {
                    _indexingObservers.Notify(document);
                }
            })),
            service.Subscribe(new ForwardingObserver<DeleteDocumentRequest>(request =>
            {
                if (routed())
                {
                    _deleteDocumentObservers.Notify(request);
                }
            })),
            service.Subscribe(new ForwardingObserver<DeleteAllDocumentsRequest>(request =>
            {
                if (routed())
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

        return await _services.RunAsync(key.Value, service => service.IndexDocumentAsync(document, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Splits the documents by provider and calls each provider's indexing service once, in the order its first
    /// document appears. Nothing is indexed when a document is null, has a null field, or has an index with no route.
    /// When a provider fails, the others are still called, and the failed <see cref="Result"/> has one error saying
    /// how many documents each provider indexed. Its <c>ids</c> metadata holds the ids not confirmed as stored, as a
    /// single provider's error does, and its reasons have one error for each provider that failed, with the provider
    /// key in <c>key</c> and that provider's ids not confirmed as stored in <c>ids</c>.
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

        var outcomes = new List<(string Key, List<SearchDocument> Documents, Result<int> Result)>();
        foreach (var (key, keyDocuments) in groups)
        {
            var result = await _services.RunAsync(key, service => service.IndexMultipleDocumentsAsync(keyDocuments, cancellationToken)).ConfigureAwait(false);
            outcomes.Add((key, keyDocuments, result));
        }

        if (outcomes.All(outcome => outcome.Result.IsSuccess))
        {
            return Result.Ok(outcomes.Sum(outcome => outcome.Result.Value));
        }

        var notStoredIds = new List<string>();
        var summaries = new List<string>();
        var reasons = new List<IError>();
        foreach (var (key, keyDocuments, result) in outcomes)
        {
            if (result.IsSuccess)
            {
                summaries.Add($"'{key}' indexed {keyDocuments.Count} of {keyDocuments.Count} documents");
                continue;
            }

            var ids = NotStoredIds(keyDocuments, result.Errors);
            notStoredIds.AddRange(ids);
            var summary = $"'{key}' indexed {keyDocuments.Count - ids.Count} of {keyDocuments.Count} documents and failed: {string.Join("; ", result.Errors.Select(e => e.Message.TrimEnd('.')))}";
            summaries.Add(summary);
            reasons.Add(new Error(summary).WithMetadata("key", key).WithMetadata("ids", ids).CausedBy(result.Errors));
        }

        return Result.Fail<int>(new Error($"Indexing the documents failed on {reasons.Count} of {outcomes.Count} providers: {string.Join("; ", summaries)}.")
            .WithMetadata("ids", notStoredIds)
            .CausedBy(reasons));
    }

    /// <summary>
    /// The ids of <paramref name="documents"/> that a failed batch did not confirm as stored: the <c>ids</c> metadata of
    /// its errors, or every id when an error has none, as when the batch failed validation or before anything was sent.
    /// </summary>
    private static List<string> NotStoredIds(List<SearchDocument> documents, IReadOnlyList<IError> errors)
    {
        var all = documents.Select(document => document.Id).Distinct(StringComparer.Ordinal).ToList();
        if (errors.Count == 0 || errors.Any(error => !error.Metadata.TryGetValue("ids", out var ids) || ids is not IEnumerable<string>))
        {
            return all;
        }

        var failed = errors.SelectMany(error => (IEnumerable<string>)error.Metadata["ids"]).ToHashSet(StringComparer.Ordinal);
        return all.Where(failed.Contains).ToList();
    }

    /// <summary>Routes by <see cref="DeleteDocumentRequest.Type"/>, which holds the index name.</summary>
    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default)
    {
        var key = _routes.KeyFor(document?.Type);
        if (key.IsFailed)
        {
            return key.ToResult();
        }

        return await _services.RunAsync(key.Value, service => service.RemoveDocumentAsync(document!, cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var key = _routes.KeyFor(index);
        if (key.IsFailed)
        {
            return key.ToResult<int>();
        }

        return await _services.RunAsync(key.Value, service => service.RemoveDocumentsAsync(index, ids, cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        var key = _routes.KeyFor(request?.IndexName);
        if (key.IsFailed)
        {
            return key.ToResult<long?>();
        }

        return await _services.RunAsync(key.Value, service => service.RemoveAllDocumentsAsync(request!, cancellationToken)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _services.Dispose();
        _indexingObservers.Complete();
        _deleteDocumentObservers.Complete();
        _deleteAllDocumentsObservers.Complete();
    }
}
