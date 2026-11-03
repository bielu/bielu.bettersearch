using Bielu.BetterSearch.Abstractions.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

public sealed class OpenSearchIndexingProviderAsync(IClientFactoryAsync<IOpenSearchClient> clientFactory, ILogger<OpenSearchIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    // OpenSearch.Client treats some 404s (for example delete by query on a missing index) as valid,
    // so a response that carries a server error is a failure too.
    private static bool IsSuccess(IResponse response) => response.IsValid && response.ServerError is null;

    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.GetOrCreateClientAsync(document.Index).ConfigureAwait(false);
        var response = await client.IndexAsync(document, i => i.Index(document.Index).Id(document.Id), cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation));
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var client = await clientFactory.GetOrCreateClientAsync(indexGroup.Key).ConfigureAwait(false);
            var response = await client.BulkAsync(b => b.Index(indexGroup.Key).IndexMany(indexGroup, (descriptor, document) => descriptor.Id(document.Id)), cancellationToken).ConfigureAwait(false);
            results.Add(IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation)));
        }

        var merged = results.Merge();
        return merged.IsSuccess ? Result.Ok(searchDocuments.Count) : Result.Fail<int>(merged.Errors);
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.GetOrCreateClientAsync(type).ConfigureAwait(false);
        var response = await client.DeleteAsync(new DeleteRequest(type, id), cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation));
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index.IndexName).ConfigureAwait(false);
        var response = await client.DeleteByQueryAsync<SearchDocument>(d => d.Index(index.IndexName).Query(q => q.MatchAll()), cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok<long?>(response.Deleted) : Result.Fail<long?>(new Error(response.DebugInformation));
    }

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        return exist is { IsSuccess: true, Value: true }
            ? Result.Ok(true)
            : await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
        var response = await client.Indices.DeleteAsync(index, ct: cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok(true) : Result.Fail<bool>(new Error(response.DebugInformation));
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var response = await client.Indices.ExistsAsync(index, ct: cancellationToken).ConfigureAwait(false);
            if (response.OriginalException is not null)
            {
                return Result.Fail<bool>(new Error(response.DebugInformation));
            }

            return Result.Ok(response.Exists);
        }
        catch (Exception e)
        {
            return Result.Fail<bool>(new Error(e.Message));
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var response = await client.Indices.CreateAsync(index, ct: cancellationToken).ConfigureAwait(false);
            if (!IsSuccess(response))
            {
                logger.LogError("Creating index {Index} failed: {Error}", index, response.DebugInformation);
            }

            return Result.Ok(IsSuccess(response));
        }
        catch (Exception e)
        {
            return Result.Fail<bool>(new Error(e.Message));
        }
    }
}
