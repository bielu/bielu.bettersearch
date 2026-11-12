using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexingProviderAsync(
    IClientFactoryAsync<IFullTextIndex<string>> clientFactory,
    ILiftiIndexManager liftiIndexManager) : IIndexingProviderAsync
{
    // Removing documents, deleting an index and removing all documents take the index's lock, so none of them sees
    // the index half-way through another. The locks are shared by every provider on the same index manager.
    private readonly LiftiIndexLocks _locks = LiftiIndexLocks.For(liftiIndexManager);

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        try
        {
            await (await clientFactory.GetOrCreateClientAsync(document.Index).ConfigureAwait(false)).AddAsync(document, cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchDocuments = documents.ToList();
            foreach (var documentGroup in searchDocuments.GroupBy(d => d.Index))
            {
                var index = await clientFactory.GetOrCreateClientAsync(documentGroup.Key).ConfigureAwait(false);
                foreach (var document in documentGroup)
                {
                    await index.AddAsync(document, cancellationToken).ConfigureAwait(false);
                }
            }
            return Result.Ok(searchDocuments.Count);
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // The lookup and the factory call below must not let a delete of this index in between, or the factory
            // would create the deleted index again.
            using var indexLock = await _locks.AcquireAsync(type, cancellationToken).ConfigureAwait(false);
            // Lifti indexes live in memory, so after a restart an index is missing until something writes to it.
            // A missing index holds no documents, so the document is already gone; the index is not created.
            var lookup = await liftiIndexManager.GetIndexAsync(type).ConfigureAwait(false);
            if (lookup.IsFailed)
            {
                return lookup.ToResult();
            }

            if (lookup.Value is not null)
            {
                // The index is taken from the client factory, as every other write does, so a custom factory is honoured.
                var index = await clientFactory.GetOrCreateClientAsync(type).ConfigureAwait(false);
                await index.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed || distinctIds.Value.Count == 0)
        {
            return distinctIds.IsFailed ? distinctIds.ToResult<int>() : Result.Ok(0);
        }

        try
        {
            // As in RemoveDocumentAsync, the lock keeps a delete of this index out from between the lookup and the
            // factory call, and a missing index holds no documents and is not created.
            using var indexLock = await _locks.AcquireAsync(index, cancellationToken).ConfigureAwait(false);
            var lookup = await liftiIndexManager.GetIndexAsync(index).ConfigureAwait(false);
            if (lookup.IsFailed)
            {
                return lookup.ToResult<int>();
            }

            if (lookup.Value is null)
            {
                return Result.Ok(0);
            }

            var liftiIndex = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var removed = 0;
            for (var i = 0; i < distinctIds.Value.Count; i++)
            {
                try
                {
                    if (await liftiIndex.RemoveAsync(distinctIds.Value[i], cancellationToken).ConfigureAwait(false))
                    {
                        removed++;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // The ids before this one are already removed; this one and the rest are not.
                    return Result.Fail<int>(DocumentIds.FailedError(index, removed, distinctIds.Value.Count,
                        distinctIds.Value.Skip(i).ToList(), e.Message).CausedBy(e));
                }
            }

            return Result.Ok(removed);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<int>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var indexLock = await _locks.AcquireAsync(index.IndexName, cancellationToken).ConfigureAwait(false);
            var count = (await clientFactory.GetOrCreateClientAsync(index.IndexName).ConfigureAwait(false)).Count;
            await liftiIndexManager.DeleteIndexAsync(index.IndexName).ConfigureAwait(false);
            await liftiIndexManager.CreateIndexAsync(index.IndexName).ConfigureAwait(false);
            return Result.Ok((long?)count);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(e.Message);
        }
    }

    public Task<Result<int>> RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        using var indexLock = await _locks.AcquireAsync(index, cancellationToken).ConfigureAwait(false);
        return await liftiIndexManager.DeleteIndexAsync(index).ConfigureAwait(false);
    }

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        liftiIndexManager.ExistsAsync(index);

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var result = await liftiIndexManager.CreateIndexAsync(index).ConfigureAwait(false);
        return result.IsSuccess ? Result.Ok(true) : Result.Fail(result.Errors[0].Message);
    }
}
