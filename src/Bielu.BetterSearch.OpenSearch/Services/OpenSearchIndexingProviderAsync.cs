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
        var client = await clientFactory.TryGetClientAsync(document.Index).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult();
        }

        var response = await client.Value.IndexAsync(document, i => i.Index(document.Index).Id(document.Id), cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation));
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var client = await clientFactory.TryGetClientAsync(indexGroup.Key).ConfigureAwait(false);
            if (client.IsFailed)
            {
                results.Add(client.ToResult());
                continue;
            }

            var response = await client.Value.BulkAsync(b => b.Index(indexGroup.Key).IndexMany(indexGroup, (descriptor, document) => descriptor.Id(document.Id)), cancellationToken).ConfigureAwait(false);
            results.Add(IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation)));
        }

        var merged = results.Merge();
        return merged.IsSuccess ? Result.Ok(searchDocuments.Count) : Result.Fail<int>(merged.Errors);
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(type).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult();
        }

        var response = await client.Value.DeleteAsync(new DeleteRequest(type, id), cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation));
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(index.IndexName).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<long?>();
        }

        var response = await client.Value.DeleteByQueryAsync<SearchDocument>(d => d.Index(index.IndexName).Query(q => q.MatchAll()), cancellationToken).ConfigureAwait(false);
        if (!IsSuccess(response))
        {
            return Result.Fail<long?>(new Error(response.DebugInformation));
        }

        // A delete by query that timed out or hit failures is still valid, but only some documents were removed.
        if (response.TimedOut || response.Failures.Count > 0)
        {
            return Result.Fail<long?>(new Error(
                $"Removing all documents from {index.IndexName} did not complete: {response.Deleted} of {response.Total} documents were removed, timed out: {response.TimedOut}, failures: {response.Failures.Count}."));
        }

        return Result.Ok<long?>(response.Deleted);
    }

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        // A failed check, such as a 401 or 5xx, is returned as it is rather than hidden behind a create attempt.
        if (exist.IsFailed)
        {
            return exist;
        }

        return exist.Value ? Result.Ok(true) : await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(index).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<bool>();
        }

        var response = await client.Value.Indices.DeleteAsync(index, ct: cancellationToken).ConfigureAwait(false);
        return IsSuccess(response) ? Result.Ok(true) : Result.Fail<bool>(new Error(response.DebugInformation));
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var response = await client.Indices.ExistsAsync(index, ct: cancellationToken).ConfigureAwait(false);
            // Only 200 and 404 answer the question; anything else, such as a 401, 403 or 5xx, is a failure.
            return response.ApiCall?.HttpStatusCode switch
            {
                200 => Result.Ok(true),
                404 => Result.Ok(false),
                _ => Result.Fail<bool>(new Error(response.DebugInformation)),
            };
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
            if (IsSuccess(response))
            {
                return Result.Ok(true);
            }

            // An index that already exists was not created, but nothing went wrong.
            if (response.ServerError?.Error?.Type == "resource_already_exists_exception")
            {
                return Result.Ok(false);
            }

            logger.LogError("Creating index {Index} failed: {Error}", index, response.DebugInformation);
            return Result.Fail<bool>(new Error(response.DebugInformation));
        }
        catch (Exception e)
        {
            return Result.Fail<bool>(new Error(e.Message));
        }
    }
}
