using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Extensions;
using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexingProviderAsync(
    IClientFactoryAsync<IFullTextIndex<string>> clientFactory,
    ILiftiIndexManager liftiIndexManager) : IIndexingProviderAsync
{
    public async Task<Result>
        IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
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

    Task<Result<int>> IIndexingProviderAsync.IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    Task<Result> IIndexingProviderAsync.
        RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Result<int>> RemoveAllDocumentsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    async Task<Result<int>> IIndexingProviderAsync.RemoveAllDocumentsAsync(string index, CancellationToken cancellationToken)
    {
        try
        {
            var count= (await clientFactory.GetOrCreateClientAsync(index)).Count;
            await liftiIndexManager.DeleteIndexAsync(index);
            await liftiIndexManager.CreateIndexAsync(index);
            return Result.Ok();
        }
        catch (Exception e)
        {
            return Result.Fail(e.Message);
        }
    }

    public async Task<Result<bool>>
        EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        (await clientFactory.GetOrCreateClientAsync(index)) != null;

    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) =>
        liftiIndexManager.DeleteIndexAsync(index);

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        liftiIndexManager.ExistsAsync(index);

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) {
        var result = await liftiIndexManager.CreateIndexAsync(index);
        return result.IsSuccess ? Result.Ok() : Result.Fail(result.Errors.First().Message);
    }

}
