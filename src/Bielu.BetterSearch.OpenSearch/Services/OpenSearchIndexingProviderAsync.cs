using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchIndexingProviderAsync(IClientFactoryAsync<IOpenSearchClient> clientFactory, ILogger<OpenSearchIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    // OpenSearch.Client treats some 404s (for example delete by query on a missing index) as valid,
    // so a response that carries a server error is a failure too.
    private static bool IsSuccess(IResponse response) => response.IsValid && response.ServerError is null;

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(document.Index, async client =>
        {
            var response = await client.IndexAsync(StoredFields.WithDatesAsText(document), i => i.Index(document.Index).Id(document.Id), cancellationToken).ConfigureAwait(false);
            return IsSuccess(response) ? Result.Ok() : Result.Fail(new Error(response.DebugInformation));
        }, cancellationToken);

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var results = new List<Result<int>>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var group = indexGroup.ToList();
            results.Add(await clientFactory.TryWithClientAsync(indexGroup.Key, async client =>
            {
                var response = await client.BulkAsync(b => b.Index(indexGroup.Key).IndexMany(group.Select(StoredFields.WithDatesAsText), (descriptor, document) => descriptor.Id(document.Id)), cancellationToken).ConfigureAwait(false);
                // A request rejected as a whole, such as a 403, has no items.
                if (response.Items is null || response.Items.Count != group.Count)
                {
                    return Result.Fail<int>(RequestFailed(OpenSearchFailures.Describe("bulk index", response), response.OriginalException));
                }

                // OpenSearch applies each item on its own, so the documents it accepted stay stored.
                var failed = response.Items.Where(item => item.Error is not null || item.Status is < 200 or >= 300).ToList();
                if (failed.Count > 0)
                {
                    return Result.Fail<int>(DocumentIds.IndexFailedError(indexGroup.Key, group.Count - failed.Count, group.Count,
                        failed.Select(item => item.Id ?? "(unknown)").ToList(), failed[0].Error?.Reason ?? $"status {failed[0].Status}"));
                }

                return Result.Ok(group.Count);
            }, cancellationToken).ConfigureAwait(false));
        }

        var merged = results.Merge();
        return merged.IsSuccess ? Result.Ok(merged.Value.Sum()) : Result.Fail<int>(merged.Errors);
    }

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(type, async client =>
        {
            var response = await client.DeleteAsync(new DeleteRequest(type, id), cancellationToken).ConfigureAwait(false);
            // Deletes are idempotent: a document that is not there is already gone. A missing index is still a failure,
            // and OpenSearch reports it with a server error rather than a not_found result.
            var removed = response.ServerError is null
                && (response.IsValid || response.Result == global::OpenSearch.Client.Result.NotFound);
            return removed ? Result.Ok() : Result.Fail(new Error(response.DebugInformation));
        }, cancellationToken);

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed || distinctIds.Value.Count == 0)
        {
            return distinctIds.IsFailed ? distinctIds.ToResult<int>() : Result.Ok(0);
        }

        return await clientFactory.TryWithClientAsync(index, async client =>
        {
            var response = await client.BulkAsync(b => b.Index(index).DeleteMany<SearchDocument>(distinctIds.Value, (descriptor, id) => descriptor.Id(id)), cancellationToken).ConfigureAwait(false);
            // A request rejected as a whole, such as a 403, has no items.
            if (response.Items is null || response.Items.Count != distinctIds.Value.Count)
            {
                return Result.Fail<int>(RequestFailed(OpenSearchFailures.Describe("bulk delete", response), response.OriginalException));
            }

            // As in RemoveDocumentAsync, a not_found item without an error is already gone. A missing index is an
            // error on each item.
            var removed = response.Items.Count(item => item.Error is null && item.Result == "deleted");
            var failed = response.Items.Where(item => item.Error is not null || (item.Status is < 200 or >= 300 && item.Result != "not_found")).ToList();
            if (failed.Count > 0)
            {
                return Result.Fail<int>(DocumentIds.FailedError(index, removed, distinctIds.Value.Count,
                    failed.Select(item => item.Id ?? "(unknown)").ToList(), failed[0].Error?.Reason ?? $"status {failed[0].Status}"));
            }

            return Result.Ok(removed);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync<long?>(index.IndexName, async client =>
        {
            var response = await client.DeleteByQueryAsync<SearchDocument>(d => d.Index(index.IndexName).Query(q => q.MatchAll()), cancellationToken).ConfigureAwait(false);
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
        }, cancellationToken);

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        // A failed check, such as a 401 or 5xx, is returned as it is rather than hidden behind a create attempt.
        if (exist.IsFailed)
        {
            return exist;
        }

        if (exist.Value)
        {
            return Result.Ok(true);
        }

        // A concurrent caller may create the index first, which CreateIndexAsync reports as Ok(false);
        // the index exists either way.
        var created = await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return created.IsSuccess ? Result.Ok(true) : created;
    }

    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var response = await client.Indices.DeleteAsync(index, ct: cancellationToken).ConfigureAwait(false);
            return IsSuccess(response) ? Result.Ok(true) : Result.Fail<bool>(new Error(response.DebugInformation));
        }, cancellationToken);

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var response = await client.Indices.ExistsAsync(index, ct: cancellationToken).ConfigureAwait(false);
            // Only 200 and 404 answer the question; anything else, such as no answer, a 401, 403 or 5xx, is a failure.
            return response.ApiCall?.HttpStatusCode switch
            {
                200 => Result.Ok(true),
                404 => Result.Ok(false),
                _ => Result.Fail<bool>(RequestFailed(OpenSearchFailures.Describe("index exists check", response), response.OriginalException)),
            };
        }, cancellationToken);

    private static Error RequestFailed(string message, Exception? exception)
    {
        var error = new Error(message);
        return exception is null ? error : error.CausedBy(exception);
    }

    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
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
        }, cancellationToken);
}
