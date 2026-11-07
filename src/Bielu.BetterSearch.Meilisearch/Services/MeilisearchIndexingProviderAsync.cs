using System.Collections.Concurrent;
using System.Text.Json;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
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

    /// <summary>
    /// The indexes whose search settings this process has applied, per client and so per server. Settings are
    /// applied on the first write to an index rather than on every one, as each update is a Meilisearch task.
    /// </summary>
    private static readonly ConcurrentDictionary<(MeilisearchClient Client, string Index), bool> ConfiguredIndexes = new();

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
        if (task.IsSuccess)
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            ConfiguredIndexes.TryRemove((client, index), out _);
        }

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
        var created = task.IsSuccess;
        if (!created)
        {
            logger.LogError("Creating index {Index} failed: {Error}", index, task.Errors[0].Message);
            // Matches the other providers: creating an index that already exists is not an error, it just creates nothing.
            if (!task.HasError(e => e.Metadata.TryGetValue("code", out var code) && Equals(code, IndexAlreadyExists)))
            {
                return Result.Fail<bool>(task.Errors);
            }
        }

        var configured = await ConfigureIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return configured.IsSuccess ? Result.Ok(created) : Result.Fail<bool>(configured.Errors);
    }

    private async Task<Result> IndexDocumentsAsync(string index, IReadOnlyCollection<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var geoField = optionsMonitor.CurrentValue.GeoField;
        var converted = documents.Select(document => MeilisearchDocument.From(document, geoField)).ToList();
        var invalid = converted.Where(document => document.IsFailed).SelectMany(document => document.Errors).ToList();
        if (invalid.Count > 0)
        {
            return Result.Fail(invalid);
        }

        // Meilisearch creates a missing index when documents are added to it, or when its settings are updated.
        var configured = await ConfigureIndexAsync(index, cancellationToken).ConfigureAwait(false);
        if (configured.IsFailed)
        {
            return configured;
        }

        var stored = converted.Select(document => document.Value).ToList();
        var task = await RunTaskAsync(index, client => client.Index(index).AddDocumentsAsync(stored, PrimaryKey, cancellationToken), cancellationToken).ConfigureAwait(false);
        return task.ToResult();
    }

    /// <summary>
    /// Makes the attributes the query translators filter and sort on filterable and sortable, and sets how deep a
    /// search can page, once per index.
    /// </summary>
    private async Task<Result> ConfigureIndexAsync(string index, CancellationToken cancellationToken)
    {
        MeilisearchClient client;
        try
        {
            client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }

        if (ConfiguredIndexes.ContainsKey((client, index)))
        {
            return Result.Ok();
        }

        var settings = new Settings
        {
            FilterableAttributes = MeilisearchFieldNames.FilterableAttributes.Select(name => new FilterableAttribute { Attribute = name }).ToList(),
            SortableAttributes = MeilisearchFieldNames.SortableAttributes,
            Pagination = new Pagination { MaxTotalHits = optionsMonitor.CurrentValue.MaxTotalHits }
        };
        var task = await RunTaskAsync(index, _ => client.Index(index).UpdateSettingsAsync(settings, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (task.IsSuccess)
        {
            ConfiguredIndexes[(client, index)] = true;
        }

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
