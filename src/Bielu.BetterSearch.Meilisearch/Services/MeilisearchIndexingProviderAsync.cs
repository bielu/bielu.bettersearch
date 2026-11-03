using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MeilisearchApiError = Meilisearch.MeilisearchApiError;
using MeilisearchClient = Meilisearch.MeilisearchClient;
using Result = FluentResults.Result;
using TaskInfo = Meilisearch.TaskInfo;
using TaskInfoStatus = Meilisearch.TaskInfoStatus;
using TaskResource = Meilisearch.TaskResource;

namespace Bielu.BetterSearch.Meilisearch.Services;

public sealed class MeilisearchIndexingProviderAsync(
    IClientFactoryAsync<MeilisearchClient> clientFactory,
    IOptionsMonitor<MeilisearchOptions> optionsMonitor,
    ILogger<MeilisearchIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    private const string PrimaryKey = "id";
    private const string IndexNotFound = "index_not_found";
    private const string IndexAlreadyExists = "index_already_exists";

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        IndexDocumentsAsync(document.Index, [document], cancellationToken);

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            results.Add(await IndexDocumentsAsync(indexGroup.Key, indexGroup.ToList(), cancellationToken).ConfigureAwait(false));
        }

        var merged = results.Merge();
        return merged.IsSuccess ? Result.Ok(searchDocuments.Count) : Result.Fail<int>(merged.Errors);
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(type, client => client.Index(type).DeleteOneDocumentAsync(id, cancellationToken), cancellationToken).ConfigureAwait(false);
        return task.ToResult();
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(index.IndexName, client => client.Index(index.IndexName).DeleteAllDocumentsAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        return task.IsSuccess ? Result.Ok(GetDeletedDocuments(task.Value)) : Result.Fail<long?>(task.Errors);
    }

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        if (exist is { IsSuccess: true, Value: true })
        {
            return Result.Ok(true);
        }

        // A concurrent caller may create the index first, which CreateIndexAsync reports as Ok(false);
        // the index exists either way.
        var created = await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return created.IsSuccess ? Result.Ok(true) : created;
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(index, client => client.DeleteIndexAsync(index, cancellationToken), cancellationToken).ConfigureAwait(false);
        return task.IsSuccess ? Result.Ok(true) : Result.Fail<bool>(task.Errors);
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            await client.GetIndexAsync(index, cancellationToken).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (MeilisearchApiError e) when (e.Code == IndexNotFound)
        {
            return Result.Ok(false);
        }
        catch (Exception e)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(index, client => client.CreateIndexAsync(index, PrimaryKey, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (task.IsSuccess)
        {
            return Result.Ok(true);
        }

        logger.LogError("Creating index {Index} failed: {Error}", index, task.Errors[0].Message);
        // Matches the other providers: creating an index that already exists is not an error, it just creates nothing.
        return task.HasError(e => e.Metadata.TryGetValue("code", out var code) && Equals(code, IndexAlreadyExists))
            ? Result.Ok(false)
            : Result.Fail<bool>(task.Errors);
    }

    private async Task<Result> IndexDocumentsAsync(string index, IReadOnlyCollection<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        // Meilisearch creates a missing index when documents are added to it.
        var task = await RunTaskAsync(index, client => client.Index(index).AddDocumentsAsync(documents, PrimaryKey, cancellationToken), cancellationToken).ConfigureAwait(false);
        return task.ToResult();
    }

    /// <summary>
    /// Starts a write and waits for the task Meilisearch queues for it, so the result reflects whether the
    /// write was applied. A task that fails carries Meilisearch's error code in the error's <c>code</c> metadata.
    /// </summary>
    private async Task<Result<TaskResource>> RunTaskAsync(string index, Func<MeilisearchClient, Task<TaskInfo>> start,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var info = await start(client).ConfigureAwait(false);
            var task = await client.WaitForTaskAsync(info.TaskUid,
                optionsMonitor.CurrentValue.TaskTimeout.TotalMilliseconds, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (task.Status == TaskInfoStatus.Succeeded)
            {
                return Result.Ok(task);
            }

            var error = new Error(task.Error?.GetValueOrDefault("message") ?? $"Meilisearch task {task.Uid} ended as {task.Status}");
            if (task.Error?.GetValueOrDefault("code") is { } code)
            {
                error.WithMetadata("code", code);
            }

            return Result.Fail<TaskResource>(error);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<TaskResource>(new Error(e.Message).CausedBy(e));
        }
    }

    private static long? GetDeletedDocuments(TaskResource task)
    {
        // Task details are deserialized as JSON elements.
        return task.Details?.GetValueOrDefault("deletedDocuments") is JsonElement { ValueKind: JsonValueKind.Number } deleted
            ? deleted.GetInt64()
            : null;
    }
}
