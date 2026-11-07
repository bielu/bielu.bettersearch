using Bielu.BetterSearch.Abstractions.Services;
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
            // Matches the other providers: a missing index is a caller mistake, not an already removed document.
            var exists = await liftiIndexManager.ExistsAsync(type).ConfigureAwait(false);
            if (!exists.Value)
            {
                return Result.Fail($"Index {type} does not exist.");
            }

            var index = await clientFactory.GetOrCreateClientAsync(type).ConfigureAwait(false);
            await index.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
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
