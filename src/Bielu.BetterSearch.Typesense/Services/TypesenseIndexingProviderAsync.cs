using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Typesense.Configuration;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Typesense.Services;

/// <summary>
/// Writes to Typesense collections. Typesense applies each write before it answers, so a document is searchable once a
/// write succeeds. A missing collection is created, with the provider's schema, by the first write to it.
/// </summary>
public sealed class TypesenseIndexingProviderAsync(
    IClientFactoryAsync<TypesenseHttpClientWrapper> clientFactory,
    IOptionsMonitor<TypesenseOptions> optionsMonitor,
    TypesenseSchema schema,
    ILogger<TypesenseIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    /// <summary>The most ids one delete by filter names.</summary>
    internal const int RemoveBatchSize = 100;

    /// <summary>
    /// The longest escaped <c>filter_by</c> one delete by filter sends, so its URL stays under common request line
    /// limits; an id longer than this alone is sent in a request of its own.
    /// </summary>
    internal const int RemoveBatchFilterLength = 4096;

    /// <summary>The most documents one import request sends.</summary>
    internal const int ImportBatchSize = 1000;

    /// <summary>
    /// The most characters of JSON one import request sends, so a large batch is never held in memory at once; a
    /// document longer than this alone is sent in a request of its own.
    /// </summary>
    internal const int ImportBatchCharacters = 4 * 1024 * 1024;

    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        IndexMultipleDocumentsAsync([document], cancellationToken).ContinueWith(task => task.Result.ToResult(), cancellationToken,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents.ToList();
        var errors = new List<IError>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var indexed = await ImportAsync(indexGroup.Key, indexGroup.ToList(), cancellationToken).ConfigureAwait(false);
            errors.AddRange(indexed.Errors);
        }

        return errors.Count == 0 ? Result.Ok(searchDocuments.Count) : Result.Fail<int>(errors);
    }

    /// <summary>
    /// Upserts <paramref name="documents"/> with import requests of up to <see cref="ImportBatchSize"/> documents and
    /// <see cref="ImportBatchCharacters"/> characters, one after another. Every document is checked before any is
    /// sent, so an invalid one stores none of them. Typesense applies each document on its own, so a rejected one
    /// leaves the others stored, and the error names the rejected ones.
    /// </summary>
    private async Task<Result> ImportAsync(string index, IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.TryCurrentValue();
        if (options.IsFailed)
        {
            return options.ToResult();
        }

        var geoField = options.Value.GeoField;
        // Each document is converted again when its request is built, so only one request's documents are held at once.
        var invalid = documents.Select(document => TypesenseDocument.From(document, geoField))
            .Where(document => document.IsFailed).SelectMany(document => document.Errors).ToList();
        if (invalid.Count > 0)
        {
            return Result.Fail(invalid);
        }

        var stored = 0;
        var failed = new List<string>();
        string? reason = null;
        var lines = new StringBuilder();
        var batch = new List<string>();
        for (var i = 0; i < documents.Count; i++)
        {
            if (batch.Count > 0)
            {
                lines.Append('\n');
            }

            lines.Append(TypesenseDocument.From(documents[i], geoField).Value.ToJsonString());
            batch.Add(documents[i].Id);
            if (i < documents.Count - 1 && batch.Count < ImportBatchSize && lines.Length < ImportBatchCharacters)
            {
                continue;
            }

            var notSent = documents.Skip(i + 1).Select(document => document.Id).ToList();
            try
            {
                var imported = await ImportBatchAsync(index, lines.ToString(), batch, cancellationToken).ConfigureAwait(false);
                if (imported.IsFailed)
                {
                    return Result.Fail(DocumentIds.IndexFailedError(index, stored, documents.Count, [.. failed, .. batch, .. notSent],
                        imported.Errors[0].Message));
                }

                stored += batch.Count - imported.Value.Failed.Count;
                failed.AddRange(imported.Value.Failed);
                reason ??= imported.Value.Reason;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The request may have reached Typesense before it failed, so any of its documents may be stored.
                List<string> ids = [.. failed, .. batch, .. notSent];
                return Result.Fail(new Error($"Indexing documents into {index} did not finish: {e.Message}. {stored} of {documents.Count} documents were indexed before the failed request, " +
                                             $"Typesense may have stored any or all of the {batch.Count} documents in it, and {notSent.Count} were not sent.")
                    .WithMetadata("ids", ids).CausedBy(e));
            }

            lines.Clear();
            batch.Clear();
        }

        return failed.Count == 0
            ? Result.Ok()
            : Result.Fail(DocumentIds.IndexFailedError(index, stored, documents.Count, failed, reason ?? string.Empty));
    }

    /// <summary>
    /// Sends one import request: the ids Typesense rejected, and its first reason, or a failure when the request as a
    /// whole failed, which stored none of them.
    /// </summary>
    private async Task<Result<(List<string> Failed, string? Reason)>> ImportBatchAsync(string index, string lines, List<string> ids,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
        var path = $"collections/{TypesenseHttpClientWrapper.Escape(index)}/documents/import?action=upsert";
        var response = await client.SendAsync(HttpMethod.Post, path, () => new StringContent(lines, Encoding.UTF8, "text/plain"), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var created = await CreateCollectionAsync(client, index, cancellationToken).ConfigureAwait(false);
            if (created.IsFailed)
            {
                return created.ToResult<(List<string>, string?)>();
            }

            response = await client.SendAsync(HttpMethod.Post, path, () => new StringContent(lines, Encoding.UTF8, "text/plain"), cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccess)
        {
            return Result.Fail<(List<string>, string?)>(response.Describe());
        }

        // The documents may have added fields, which a search should see at once.
        schema.Forget(client, index);

        var failed = new List<string>();
        string? reason = null;
        var results = response.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < ids.Count; i++)
        {
            using var line = JsonDocument.Parse(i < results.Length ? results[i] : "{}");
            if (line.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                continue;
            }

            failed.Add(ids[i]);
            reason ??= line.RootElement.TryGetProperty("error", out var error) ? error.GetString() : "Typesense did not report the document's outcome";
        }

        return Result.Ok((failed, reason));
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(type).ConfigureAwait(false);
            // A '.' or '..' path segment would be resolved away, so such an id is deleted by filter instead.
            var path = id is "." or ".."
                ? $"collections/{TypesenseHttpClientWrapper.Escape(type)}/documents?filter_by={Uri.EscapeDataString($"id:[`{id}`]")}"
                : $"collections/{TypesenseHttpClientWrapper.Escape(type)}/documents/{TypesenseHttpClientWrapper.Escape(id)}";
            var response = await client.SendAsync(HttpMethod.Delete, path, null, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccess)
            {
                return Result.Ok();
            }

            // A missing document is already removed; a missing collection is a caller mistake, as on the other engines.
            if (response.StatusCode == HttpStatusCode.NotFound && !IsMissingCollection(response))
            {
                return Result.Ok();
            }

            return Result.Fail(new Error($"Removing document {id} from {type} failed: {response.Describe()}"));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = DocumentIds.Distinct(ids);
        if (distinctIds.IsFailed || distinctIds.Value.Count == 0)
        {
            return distinctIds.IsFailed ? distinctIds.ToResult<int>() : Result.Ok(0);
        }

        if (distinctIds.Value.FirstOrDefault(id => id.Contains('`', StringComparison.Ordinal)) is { } unquotable)
        {
            return Result.Fail<int>(DocumentIds.FailedError(index, 0, distinctIds.Value.Count, distinctIds.Value,
                $"id '{unquotable}' holds a backtick, which a Typesense filter cannot contain"));
        }

        var removed = 0;
        var batches = RemoveBatches(distinctIds.Value);
        for (var i = 0; i < batches.Count; i++)
        {
            var remaining = batches.Skip(i).SelectMany(batch => batch).ToList();
            try
            {
                var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
                var filter = RemoveFilter(batches[i]);
                var response = await client.SendAsync(HttpMethod.Delete,
                    $"collections/{TypesenseHttpClientWrapper.Escape(index)}/documents?filter_by={filter}", null, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccess)
                {
                    return Result.Fail<int>(DocumentIds.FailedError(index, removed, distinctIds.Value.Count, remaining, response.Describe()));
                }

                removed += (int)DeletedCount(response);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return Result.Fail<int>(DocumentIds.FailedError(index, removed, distinctIds.Value.Count, remaining, e.Message).CausedBy(e));
            }
        }

        return Result.Ok(removed);
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index.IndexName).ConfigureAwait(false);
            var response = await client.SendAsync(HttpMethod.Delete,
                $"collections/{TypesenseHttpClientWrapper.Escape(index.IndexName)}/documents?truncate=true", null, cancellationToken).ConfigureAwait(false);
            return response.IsSuccess
                ? Result.Ok<long?>(DeletedCount(response))
                : Result.Fail<long?>($"Removing every document from {index.IndexName} failed: {response.Describe()}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<long?>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken).ConfigureAwait(false);
        // A failed check, such as a 401 or an unreachable server, is returned as it is rather than hidden behind a create attempt.
        if (exist.IsFailed || exist.Value)
        {
            return exist;
        }

        // A concurrent caller may create the collection first, which CreateIndexAsync reports as Ok(false); it exists either way.
        var created = await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return created.IsSuccess ? Result.Ok(true) : created;
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            schema.Forget(client, index);
            var response = await client.SendAsync(HttpMethod.Delete, $"collections/{TypesenseHttpClientWrapper.Escape(index)}", null, cancellationToken).ConfigureAwait(false);
            return response.IsSuccess
                ? Result.Ok(true)
                : Result.Fail<bool>($"Deleting index {index} failed: {response.Describe()}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var response = await client.SendAsync(HttpMethod.Get, $"collections/{TypesenseHttpClientWrapper.Escape(index)}", null, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccess)
            {
                return Result.Ok(true);
            }

            return response.StatusCode == HttpStatusCode.NotFound
                ? Result.Ok(false)
                : Result.Fail<bool>($"Checking index {index} failed: {response.Describe()}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            return await CreateCollectionAsync(client, index, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<bool>(new Error(e.Message).CausedBy(e));
        }
    }

    /// <summary>Creates the collection; <c>Ok(false)</c> when it already exists, as on the other providers.</summary>
    private async Task<Result<bool>> CreateCollectionAsync(TypesenseHttpClientWrapper client, string index, CancellationToken cancellationToken)
    {
        schema.Forget(client, index);
        var body = TypesenseSchema.Create(index).ToJsonString();
        var response = await client.SendAsync(HttpMethod.Post, "collections",
            () => new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
        if (response.IsSuccess)
        {
            return Result.Ok(true);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return Result.Ok(false);
        }

        logger.LogError("Creating index {Index} failed: {Error}", index, response.Describe());
        return Result.Fail<bool>($"Creating index {index} failed: {response.Describe()}");
    }

    private static bool IsMissingCollection(TypesenseResponse response) =>
        response.Message.Contains("Collection not found", StringComparison.OrdinalIgnoreCase);

    private static long DeletedCount(TypesenseResponse response)
    {
        using var json = JsonDocument.Parse(response.Body);
        return json.RootElement.TryGetProperty("num_deleted", out var deleted) ? deleted.GetInt64() : 0;
    }

    /// <summary>The escaped <c>filter_by</c> that deletes <paramref name="ids"/>.</summary>
    private static string RemoveFilter(IEnumerable<string> ids) =>
        Uri.EscapeDataString($"id:[{string.Join(',', ids.Select(id => $"`{id}`"))}]");

    /// <summary>
    /// The ids in batches of up to <see cref="RemoveBatchSize"/> whose filter stays within
    /// <see cref="RemoveBatchFilterLength"/> characters; an id too long for that is a batch of its own.
    /// </summary>
    internal static List<List<string>> RemoveBatches(IReadOnlyList<string> ids)
    {
        var batches = new List<List<string>>();
        var batch = new List<string>();
        var empty = RemoveFilter([]).Length;
        var length = empty;
        foreach (var id in ids)
        {
            // The escaped id, its backticks and a comma.
            var idLength = Uri.EscapeDataString($"`{id}`,").Length;
            if (batch.Count > 0 && (batch.Count == RemoveBatchSize || length + idLength > RemoveBatchFilterLength))
            {
                batches.Add(batch);
                batch = [];
                length = empty;
            }

            batch.Add(id);
            length += idLength;
        }

        if (batch.Count > 0)
        {
            batches.Add(batch);
        }

        return batches;
    }
}
