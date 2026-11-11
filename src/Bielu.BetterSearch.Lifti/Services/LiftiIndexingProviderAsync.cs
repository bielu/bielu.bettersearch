using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexingProviderAsync(
    IClientFactoryAsync<IFullTextIndex<string>> clientFactory,
    ILiftiIndexManager liftiIndexManager) : IIndexingProviderAsync
{
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
        try
        {
            // Lifti indexes live in memory, so after a restart an index is missing until something writes to it.
            // A missing index holds no documents, so the document is already gone; the index is not created.
            var index = await liftiIndexManager.GetIndexAsync(type).ConfigureAwait(false);
            if (index.IsFailed)
            {
                return index.ToResult();
            }

            if (index.Value is not null)
            {
                await index.Value.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok();
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed || distinctIds.Value.Count == 0)
        {
            return distinctIds.IsFailed ? distinctIds.ToResult<int>() : Result.Ok(0);
        }

        try
        {
            // As in RemoveDocumentAsync, a missing index holds no documents and is not created.
            var liftiIndex = await liftiIndexManager.GetIndexAsync(index).ConfigureAwait(false);
            if (liftiIndex.IsFailed)
            {
                return liftiIndex.ToResult<int>();
            }

            var removed = 0;
            for (var i = 0; liftiIndex.Value is not null && i < distinctIds.Value.Count; i++)
            {
                try
                {
                    if (await liftiIndex.Value.RemoveAsync(distinctIds.Value[i], cancellationToken).ConfigureAwait(false))
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
            var count = (await clientFactory.GetOrCreateClientAsync(index.IndexName).ConfigureAwait(false)).Count;
            await liftiIndexManager.DeleteIndexAsync(index.IndexName).ConfigureAwait(false);
            await liftiIndexManager.CreateIndexAsync(index.IndexName).ConfigureAwait(false);
            return Result.Ok((long?)count);
        }
        catch (Exception e)
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

    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) =>
        liftiIndexManager.DeleteIndexAsync(index);

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        liftiIndexManager.ExistsAsync(index);

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var result = await liftiIndexManager.CreateIndexAsync(index).ConfigureAwait(false);
        return result.IsSuccess ? Result.Ok(true) : Result.Fail(result.Errors[0].Message);
    }
}
