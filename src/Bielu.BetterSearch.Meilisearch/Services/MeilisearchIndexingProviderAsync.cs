using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FilterableAttribute = Meilisearch.FilterableAttribute;
using MeilisearchApiError = Meilisearch.MeilisearchApiError;
using MeilisearchClient = Meilisearch.MeilisearchClient;
using Pagination = Meilisearch.Pagination;
using Result = FluentResults.Result;
using Settings = Meilisearch.Settings;
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
    private const string QueuedTaskUid = "taskUid";

    // When each index's settings were last found complete, and for which MaxTotalHits, per client. The singleton
    // client factory caches the clients, so this lives as long as they do and is shared by every scope.
    private static readonly ConditionalWeakTable<MeilisearchClient, ConcurrentDictionary<string, (long CheckedAt, int MaxTotalHits)>> SettingsChecks = new();

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        IndexDocumentsAsync(document.Index, [document], cancellationToken);

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var group = indexGroup.ToList();
            var (indexed, mayBeStored) = await AddDocumentsAsync(indexGroup.Key, group, cancellationToken).ConfigureAwait(false);
            results.Add(indexed.IsSuccess ? indexed : Result.Fail(IndexFailedError(indexGroup.Key, group, indexed.Errors, mayBeStored)));
        }

        var merged = results.Merge();
        return merged.IsSuccess ? Result.Ok(searchDocuments.Count) : Result.Fail<int>(merged.Errors);
    }

    /// <summary>
    /// The error for one index of a bulk write. Meilisearch adds an index's documents in one task, which succeeds or
    /// fails whole, so none of <paramref name="documents"/> is stored, unless <paramref name="mayBeStored"/> says the
    /// add request was sent and its outcome is unknown.
    /// </summary>
    private static Error IndexFailedError(string index, IReadOnlyCollection<SearchDocument> documents, IReadOnlyList<IError> errors, bool mayBeStored)
    {
        var ids = documents.Select(document => document.Id).ToList();
        var error = !mayBeStored
            ? DocumentIds.IndexFailedError(index, 0, ids.Count, ids, string.Join("; ", errors.Select(e => e.Message)))
            : errors[0].Metadata.TryGetValue(QueuedTaskUid, out var taskUid)
                ? new Error($"Indexing documents into {index} did not finish: {errors[0].Message}. Meilisearch task {taskUid} was queued and may still index any or all of the {ids.Count} documents.")
                    .WithMetadata("ids", ids)
                : new Error($"Indexing documents into {index} did not finish: {errors[0].Message}. Meilisearch may have received the request before it failed, so it may still index any or all of the {ids.Count} documents.")
                    .WithMetadata("ids", ids);
        // Keeps Meilisearch's code in the code metadata, as the other writes do. A taskUid is kept only for the
        // document task: one from the settings update would point at a task that indexes nothing.
        return error.WithMetadata(errors[0].Metadata.Where(entry => entry.Key != "ids" && (mayBeStored || entry.Key != QueuedTaskUid)).ToDictionary())
            .CausedBy(errors);
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(type, client => client.Index(type).DeleteOneDocumentAsync(id, cancellationToken), cancellationToken).ConfigureAwait(false);
        return task.ToResult();
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed || distinctIds.Value.Count == 0)
        {
            return distinctIds.IsFailed ? distinctIds.ToResult<int>() : Result.Ok(0);
        }

        // One deletion task: Meilisearch applies it whole or fails it whole, so there are no per-id failures.
        var task = await RunTaskAsync(index, client => client.Index(index).DeleteDocumentsAsync(distinctIds.Value, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (task.IsFailed)
        {
            // A task that was queued but not seen to finish may still remove any or all of the ids, so the count is
            // unknown. Otherwise nothing was queued, or the task failed whole, and nothing was removed.
            var error = task.Errors[0].Metadata.TryGetValue(QueuedTaskUid, out var taskUid)
                ? new Error($"Removing documents from {index} did not finish: {task.Errors[0].Message}. Meilisearch task {taskUid} was queued and may still remove any or all of the {distinctIds.Value.Count} documents.")
                    .WithMetadata("ids", distinctIds.Value.ToList())
                : DocumentIds.FailedError(index, 0, distinctIds.Value.Count, distinctIds.Value, task.Errors[0].Message);
            // Keeps Meilisearch's code in the code metadata, as the other writes do.
            error.WithMetadata(task.Errors[0].Metadata.Where(entry => entry.Key != "ids").ToDictionary())
                .CausedBy(task.Errors);
            return Result.Fail<int>(error);
        }

        return GetDeletedDocuments(task.Value) is { } deleted
            ? Result.Ok((int)deleted)
            : Result.Fail<int>($"Meilisearch task {task.Value.Uid} removed documents from {index} but did not report how many.");
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
        // A failed check, such as a 401 or an unreachable server, is returned as it is rather than hidden behind a create attempt.
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

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(index, client =>
        {
            ForgetSettingsCheck(client, index);
            return client.DeleteIndexAsync(index, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
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
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var task = await RunTaskAsync(index, client =>
        {
            // A created index has no settings, so an earlier check, made before it was deleted elsewhere, no longer
            // holds. Forgetting it when the index already exists only costs one more settings read.
            ForgetSettingsCheck(client, index);
            return client.CreateIndexAsync(index, PrimaryKey, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
        var created = task.IsSuccess;
        // Matches the other providers: creating an index that already exists is not an error, it just creates nothing.
        if (!created && !task.HasError(e => e.Metadata.TryGetValue("code", out var code) && Equals(code, IndexAlreadyExists)))
        {
            logger.LogError("Creating index {Index} failed: {Error}", index, task.Errors[0].Message);
            return Result.Fail<bool>(task.Errors);
        }

        var configured = await ConfigureIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return configured.IsSuccess ? Result.Ok(created) : Result.Fail<bool>(configured.Errors);
    }

    private async Task<Result> IndexDocumentsAsync(string index, IReadOnlyCollection<SearchDocument> documents,
        CancellationToken cancellationToken) =>
        (await AddDocumentsAsync(index, documents, cancellationToken).ConfigureAwait(false)).Result;

    /// <summary>
    /// Adds <paramref name="documents"/> to <paramref name="index"/> in one task. <c>MayBeStored</c> is true when the
    /// add request was sent and failed without a definite answer: the task was queued but not seen to finish, or the
    /// request failed without a Meilisearch response, so Meilisearch may still store the documents. A failure before
    /// the request, a rejected request and a failed task store nothing.
    /// </summary>
    private async Task<(Result Result, bool MayBeStored)> AddDocumentsAsync(string index, IReadOnlyCollection<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var currentOptions = optionsMonitor.TryCurrentValue();
        if (currentOptions.IsFailed)
        {
            return (currentOptions.ToResult(), false);
        }

        var options = currentOptions.Value;
        var phraseFields = options.PhraseFields?.ToHashSet(StringComparer.Ordinal);
        var converted = documents.Select(document => MeilisearchDocument.From(document, options.GeoField, phraseFields)).ToList();
        var invalid = converted.Where(document => document.IsFailed).SelectMany(document => document.Errors).ToList();
        if (invalid.Count > 0)
        {
            return (Result.Fail(invalid), false);
        }

        // Meilisearch creates a missing index when documents are added to it, or when its settings are updated.
        var configured = await ConfigureIndexAsync(index, cancellationToken).ConfigureAwait(false);
        if (configured.IsFailed)
        {
            return (configured, false);
        }

        var stored = converted.Select(document => document.Value).ToList();
        var sent = false;
        var task = await RunTaskAsync(index, client =>
        {
            sent = true;
            return client.Index(index).AddDocumentsAsync(stored, PrimaryKey, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
        // A MeilisearchApiError is Meilisearch's answer that it rejected the request; any other exception after the
        // request started, such as a dropped connection, leaves the outcome unknown.
        var mayBeStored = task.IsFailed && sent
            && (task.Errors[0].Metadata.ContainsKey(QueuedTaskUid)
                || task.Errors[0].Reasons.OfType<ExceptionalError>().Any(reason => reason.Exception is not MeilisearchApiError));
        return (task.ToResult(), mayBeStored);
    }

    /// <summary>
    /// Makes the attributes the query translators filter and sort on filterable and sortable, and lets a search page
    /// at least <c>MaxTotalHits</c> deep. Each update is a Meilisearch task, so the settings are read first and
    /// updated only when something is missing. Once complete they are trusted for <c>MeilisearchOptions.SettingsCheckInterval</c>,
    /// so a burst of writes reads them once; an index deleted or changed outside the provider is fixed by the first
    /// write after that, and a higher <c>MaxTotalHits</c> by the next write. Attributes added by others and a higher
    /// <c>maxTotalHits</c> are kept, so two configurations sharing an index never undo each other's settings.
    /// </summary>
    private async Task<Result> ConfigureIndexAsync(string index, CancellationToken cancellationToken)
    {
        var currentOptions = optionsMonitor.TryCurrentValue();
        if (currentOptions.IsFailed)
        {
            return currentOptions.ToResult();
        }

        var options = currentOptions.Value;
        var maxTotalHits = options.MaxTotalHits;
        MeilisearchClient client;
        try
        {
            client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }

        var checks = SettingsChecks.GetValue(client, _ => new ConcurrentDictionary<string, (long, int)>(StringComparer.Ordinal));
        if (checks.TryGetValue(index, out var last) && last.MaxTotalHits >= maxTotalHits
            && Stopwatch.GetElapsedTime(last.CheckedAt) < options.SettingsCheckInterval)
        {
            return Result.Ok();
        }

        Settings? current = null;
        try
        {
            current = await client.Index(index).GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MeilisearchApiError e) when (e.Code == IndexNotFound)
        {
            // Updating the settings creates the index.
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }

        var filterable = current?.FilterableAttributes?.ToList() ?? [];
        var missingFilterable = MeilisearchFieldNames.FilterableAttributes
            .Except(filterable.Select(attribute => attribute.Attribute).OfType<string>(), StringComparer.Ordinal).ToList();
        var sortable = current?.SortableAttributes?.ToList() ?? [];
        var missingSortable = MeilisearchFieldNames.SortableAttributes.Except(sortable, StringComparer.Ordinal).ToList();
        var currentMaxTotalHits = current?.Pagination?.MaxTotalHits ?? 0;
        var configuredMaxTotalHits = Math.Max(currentMaxTotalHits, maxTotalHits);
        if (missingFilterable.Count > 0 || missingSortable.Count > 0 || currentMaxTotalHits < maxTotalHits)
        {
            var settings = new Settings
            {
                FilterableAttributes = filterable.Concat(missingFilterable.Select(name => new FilterableAttribute { Attribute = name })).ToList(),
                SortableAttributes = sortable.Concat(missingSortable).ToList(),
                Pagination = new Pagination { MaxTotalHits = configuredMaxTotalHits }
            };
            var task = await RunTaskAsync(index, c => c.Index(index).UpdateSettingsAsync(settings, cancellationToken), cancellationToken).ConfigureAwait(false);
            if (task.IsFailed)
            {
                return task.ToResult();
            }
        }

        checks[index] = (Stopwatch.GetTimestamp(), configuredMaxTotalHits);
        return Result.Ok();
    }

    private static void ForgetSettingsCheck(MeilisearchClient client, string index)
    {
        if (SettingsChecks.TryGetValue(client, out var checks))
        {
            checks.TryRemove(index, out _);
        }
    }

    /// <summary>
    /// Starts a write and waits for the task Meilisearch queues for it, so the result reflects whether the
    /// write was applied. A task that fails carries Meilisearch's error code in the error's <c>code</c> metadata.
    /// A task that was queued but not seen to finish, such as on a <see cref="MeilisearchOptions.TaskTimeout"/>,
    /// carries its uid in the <c>taskUid</c> metadata, because Meilisearch may still apply it.
    /// </summary>
    private async Task<Result<TaskResource>> RunTaskAsync(string index, Func<MeilisearchClient, Task<TaskInfo>> start,
        CancellationToken cancellationToken)
    {
        TaskInfo? info = null;
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            info = await start(client).ConfigureAwait(false);
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
            var error = new Error(e.Message).CausedBy(e);
            return Result.Fail<TaskResource>(info is null ? error : error.WithMetadata(QueuedTaskUid, info.TaskUid));
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
