using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;

namespace Bielu.BetterSearch.Services;

public class IndexingServiceAsync(IIndexingProviderAsync indexingProvider, IDocumentValidatorAsync documentValidator)
    : IIndexingServiceAsync, IDisposable
{
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<IObserver<SearchDocument>> _indexingObservers = new();

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<IObserver<DeleteDocumentRequest>> _deleteDocumentObservers = new();

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private List<IObserver<DeleteAllDocumentsRequest>> _deleteAllDocumentsObservers = new();


    public IDisposable Subscribe(IObserver<SearchDocument> observer)
    {
        if (!_indexingObservers.Contains(observer))
        {
            _indexingObservers.Add(observer);
        }

        return new Unsubscriber<SearchDocument>(_indexingObservers, observer);
    }

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var validationResult = await documentValidator.ValidateDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        if (validationResult.IsFailed)
        {
            return Result.Fail("Validation failed");
        }

        if (validationResult.Value == false)
        {
            return Result.Fail("Document is not valid");
        }

        var result = await indexingProvider.IndexDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            Notify(_indexingObservers, document);
        }

        return result;
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document,
        CancellationToken cancellationToken = default)
    {
        var documents = document.ToList();
        foreach (var doc in documents)
        {
            var result = await IndexDocumentAsync(doc, cancellationToken).ConfigureAwait(false);
            if (result.IsFailed)
            {
                return Result.Fail<int>(result.Errors);
            }
        }
        return Result.Ok(documents.Count);
    }

    public async Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document,
        CancellationToken cancellationToken = default)
    {
        var result = await indexingProvider.RemoveDocumentAsync(document.Id, document.Type, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            Notify(_deleteDocumentObservers, document);
        }

        return result;
    }


    public async Task<Result<int>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await indexingProvider.RemoveAllDocumentsAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            Notify(_deleteAllDocumentsObservers, request);
        }

        return result.ToResult();
    }

    public IDisposable Subscribe(IObserver<DeleteDocumentRequest> observer)
    {
        if (!_deleteDocumentObservers.Contains(observer))
        {
            _deleteDocumentObservers.Add(observer);
        }

        return new Unsubscriber<DeleteDocumentRequest>(_deleteDocumentObservers, observer);
    }

    public IDisposable Subscribe(IObserver<DeleteAllDocumentsRequest> observer)
    {
        if (!_deleteAllDocumentsObservers.Contains(observer))
        {
            _deleteAllDocumentsObservers.Add(observer);
        }

        return new Unsubscriber<DeleteAllDocumentsRequest>(_deleteAllDocumentsObservers, observer);
    }

    /// <summary>Ends every stream: each observer gets <see cref="IObserver{T}.OnCompleted"/> once and is unsubscribed.</summary>
#pragma warning disable CA1816
    public void Dispose()
#pragma warning restore CA1816
    {
        Complete(_indexingObservers);
        Complete(_deleteDocumentObservers);
        Complete(_deleteAllDocumentsObservers);
    }

    // Iterate a copy, so an observer can unsubscribe from inside its own callback. Skip any observer that left
    // during this dispatch, so a Dispose from inside OnNext can't be followed by OnNext to an already completed observer.
    private static void Notify<T>(List<IObserver<T>> observers, T value)
    {
        foreach (var observer in observers.ToArray())
        {
            if (observers.Contains(observer))
            {
                observer.OnNext(value);
            }
        }
    }

    private static void Complete<T>(List<IObserver<T>> observers)
    {
        var completed = observers.ToArray();
        observers.Clear();
        foreach (var observer in completed)
        {
            observer.OnCompleted();
        }
    }
}
