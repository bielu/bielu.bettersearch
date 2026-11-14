using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using Refresh = OpenSearch.Net.Refresh;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.OpenSearch.Services;

internal sealed class OpenSearchIndexingProviderAsync(
    IClientFactoryAsync<IOpenSearchClient> clientFactory,
    ILogger<OpenSearchIndexingProviderAsync> logger,
    IOpenSearchClientSettingsManager settingsManager) : IIndexingProviderAsync
{
    // OpenSearch.Client treats some 404s (for example delete by query on a missing index) as valid,
    // so a response that carries a server error is a failure too.
    private static bool IsSuccess(IResponse response) => response.IsValid && response.ServerError is null;

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(document.Index, async client =>
        {
            var response = await client.IndexAsync(StoredFields.WithDatesAsText(document), i => i.Index(document.Index).Id(document.Id).Refresh(WaitFor(document.Index)), cancellationToken).ConfigureAwait(false);
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
            results.Add(await clientFactory.TryWithClientAsync(indexGroup.Key, client =>
                BulkBatches.IndexAsync(indexGroup.Key, group, settingsManager.GetConnectionSettings(indexGroup.Key).BulkBatchSize,
                    OpenSearchOptions.OpenSearchOptionsKey, async batch =>
                    {
                        var response = await client.BulkAsync(b => b.Index(indexGroup.Key).IndexMany(batch.Select(StoredFields.WithDatesAsText), (descriptor, document) => descriptor.Id(document.Id)).Refresh(WaitFor(indexGroup.Key)), cancellationToken).ConfigureAwait(false);
                        // A request rejected as a whole, such as a 403, has no items.
                        if (response.Items is null || response.Items.Count != batch.Length)
                        {
                            return BulkBatches.Outcome.Failed(RequestFailed(OpenSearchFailures.Describe("bulk index", response), response.OriginalException));
                        }

                        // OpenSearch applies each item on its own, so the documents it accepted stay stored.
                        var failed = response.Items.Where(item => item.Error is not null || item.Status is < 200 or >= 300).ToList();
                        return BulkBatches.Outcome.Items(failed.Select(item => item.Id ?? "(unknown)").ToList(),
                            failed.Count == 0 ? null : failed[0].Error?.Reason ?? $"status {failed[0].Status}");
                    }), cancellationToken).ConfigureAwait(false));
        }

        var merged = results.Merge();
        return merged.IsSuccess ? Result.Ok(merged.Value.Sum()) : Result.Fail<int>(merged.Errors);
    }

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(type, async client =>
        {
            var response = await client.DeleteAsync(new DeleteRequest(type, id) { Refresh = WaitFor(type) }, cancellationToken).ConfigureAwait(false);
            // Deletes are idempotent: a document that is not there is already gone. A missing index is still a failure,
            // and OpenSearch reports it with a server error rather than a not_found result.
            var removed = IsSuccess(response)
                || (response.ServerError is null && response.Result == global::OpenSearch.Client.Result.NotFound);
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
            var response = await client.BulkAsync(b => b.Index(index).DeleteMany<SearchDocument>(distinctIds.Value, (descriptor, id) => descriptor.Id(id)).Refresh(WaitFor(index)), cancellationToken).ConfigureAwait(false);
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
            // Delete by query selects only the documents that are searchable when it starts, so refresh first to
            // include every acknowledged write, whatever the write visibility.
            var refreshed = await client.Indices.RefreshAsync(index.IndexName, ct: cancellationToken).ConfigureAwait(false);
            if (!IsSuccess(refreshed))
            {
                return Result.Fail<long?>(RequestFailed(OpenSearchFailures.Describe($"refresh of index {index.IndexName} before removing all documents", refreshed), refreshed.OriginalException));
            }

            if (refreshed.Shards?.Failed > 0)
            {
                return Result.Fail<long?>(RefreshShardsFailed(index.IndexName, refreshed.Shards.Failed, refreshed.Shards.Total));
            }

            // Delete by query can't wait for a refresh, so it refreshes the index itself.
            var response = await client.DeleteByQueryAsync<SearchDocument>(d => d.Index(index.IndexName).Query(q => q.MatchAll()).Refresh(WaitsUntilSearchable(index.IndexName) ? Refresh.True : null), cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Creates the index with its configured mappings when it is missing. When it exists, adds the configured mappings
    /// to it, which fails when one conflicts with how the field is already mapped. <c>true</c> whenever the index exists
    /// afterwards, whether it already existed, this call created it, or another caller created it first.
    /// </summary>
    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        // A failed check, such as a 401 or 5xx, is returned as it is rather than hidden behind a create attempt.
        if (exist.IsFailed)
        {
            return exist;
        }

        if (!exist.Value)
        {
            var created = await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
            if (created.IsFailed)
            {
                return created;
            }

            // Ok(true) means this call created the index with the mappings. Ok(false) means another caller
            // created it in between, perhaps without them, so they still have to be added.
            if (created.Value)
            {
                return Result.Ok(true);
            }
        }

        // The index exists either way; only a failure to add the mappings is a failure.
        var mapped = await PutMappingsAsync(index, cancellationToken).ConfigureAwait(false);
        return mapped.IsFailed ? mapped : Result.Ok(true);
    }

    private Task<Result<bool>> PutMappingsAsync(string index, CancellationToken cancellationToken) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var properties = OpenSearchMappings.Create(settingsManager.GetConnectionSettings(index).Mappings);
            if (properties is null)
            {
                return Result.Ok(false);
            }

            var response = await client.Indices.PutMappingAsync(new PutMappingRequest(index) { Properties = properties }, cancellationToken).ConfigureAwait(false);
            // A 200 with acknowledged false means the cluster took the mappings but did not confirm them in time. The client
            // calls that response invalid, so it is told apart before the generic failure.
            if (response.ApiCall is { Success: true } && response.OriginalException is null && response.ServerError is null && !response.Acknowledged)
            {
                return Result.Fail<bool>(NotAcknowledged(index));
            }

            return IsSuccess(response)
                ? Result.Ok(true)
                : Result.Fail<bool>(RequestFailed(OpenSearchFailures.Describe($"adding the configured mappings to index {index}", response), response.OriginalException));
        }, cancellationToken);

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

    private static string NotAcknowledged(string index) =>
        $"Adding the configured mappings to index {index} was not acknowledged in time. The mappings are not rolled back and may still apply; call EnsureIndexExistsAsync again to confirm them.";

    private bool WaitsUntilSearchable(string index) =>
        settingsManager.GetConnectionSettings(index).WriteVisibility == WriteVisibility.WaitUntilSearchable;

    /// <summary><c>refresh=wait_for</c> when the index's writes wait until they are searchable; otherwise the engine's default.</summary>
    private Refresh? WaitFor(string index) => WaitsUntilSearchable(index) ? Refresh.WaitFor : null;

    private static string RefreshShardsFailed(string index, long failed, long total) =>
        $"Refreshing index {index} before removing all documents failed on {failed} of {total} shards, so no documents were removed.";

    private static Error RequestFailed(string message, Exception? exception)
    {
        var error = new Error(message);
        return exception is null ? error : error.CausedBy(exception);
    }

    /// <summary>
    /// Creates the index with its configured mappings. <c>false</c> when it already exists, which leaves its mappings as
    /// they are; any other refusal, such as an unknown analyzer, is a failure.
    /// </summary>
    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var request = new CreateIndexRequest(index);
            var properties = OpenSearchMappings.Create(settingsManager.GetConnectionSettings(index).Mappings);
            if (properties is not null)
            {
                request.Mappings = new TypeMapping { Properties = properties };
            }

            var response = await client.Indices.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a failed response; check before logging it as an error.
            cancellationToken.ThrowIfCancellationRequested();
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
            return Result.Fail<bool>(RequestFailed(OpenSearchFailures.Describe("create index", response), response.OriginalException));
        }, cancellationToken);
}
