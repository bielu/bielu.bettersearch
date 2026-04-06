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
            await (await clientFactory.GetOrCreateClientAsync(document.Index)).AddAsync(document, cancellationToken);
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
                var index = await clientFactory.GetOrCreateClientAsync(documentGroup.Key);
                foreach (var document in documentGroup)
                {
                    await index.AddAsync(document, cancellationToken);
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
            var index = await clientFactory.GetOrCreateClientAsync(type);
            await index.RemoveAsync(id, cancellationToken);
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
            var count = (await clientFactory.GetOrCreateClientAsync(index.IndexName)).Count;
            await liftiIndexManager.DeleteIndexAsync(index.IndexName);
            await liftiIndexManager.CreateIndexAsync(index.IndexName);
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
            await clientFactory.GetOrCreateClientAsync(index);
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
        var result = await liftiIndexManager.CreateIndexAsync(index);
        return result.IsSuccess ? Result.Ok(true) : Result.Fail(result.Errors.First().Message);
    }
}
