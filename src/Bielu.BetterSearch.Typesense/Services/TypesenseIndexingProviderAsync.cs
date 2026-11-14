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
    /// <summary>How many ids one delete by filter names, so its URL stays a bounded size.</summary>
    internal const int RemoveBatchSize = 100;

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
    /// Upserts <paramref name="documents"/> with one import request. Typesense applies each document on its own, so a
    /// rejected one leaves the others stored, and the error names the rejected ones.
    /// </summary>
    private async Task<Result> ImportAsync(string index, IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken)
    {
        var geoField = optionsMonitor.CurrentValue.GeoField;
        var converted = documents.Select(document => TypesenseDocument.From(document, geoField)).ToList();
        var invalid = converted.Where(document => document.IsFailed).SelectMany(document => document.Errors).ToList();
        if (invalid.Count > 0)
        {
            return Result.Fail(invalid);
        }

        var ids = documents.Select(document => document.Id).ToList();
        var lines = string.Join('\n', converted.Select(document => document.Value.ToJsonString()));
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
            var path = $"collections/{TypesenseHttpClientWrapper.Escape(index)}/documents/import?action=upsert";
            var response = await client.SendAsync(HttpMethod.Post, path, () => new StringContent(lines, Encoding.UTF8, "text/plain"), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var created = await CreateCollectionAsync(client, index, cancellationToken).ConfigureAwait(false);
                if (created.IsFailed)
                {
                    return Result.Fail(DocumentIds.IndexFailedError(index, 0, ids.Count, ids, created.Errors[0].Message));
                }

                response = await client.SendAsync(HttpMethod.Post, path, () => new StringContent(lines, Encoding.UTF8, "text/plain"), cancellationToken).ConfigureAwait(false);
            }

            if (!response.IsSuccess)
            {
                return Result.Fail(DocumentIds.IndexFailedError(index, 0, ids.Count, ids, response.Describe()));
            }

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

            return failed.Count == 0
                ? Result.Ok()
                : Result.Fail(DocumentIds.IndexFailedError(index, ids.Count - failed.Count, ids.Count, failed, reason ?? string.Empty));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The request may have reached Typesense before it failed, so any of the documents may be stored.
            return Result.Fail(new Error($"Indexing documents into {index} did not finish: {e.Message}. Typesense may have stored any or all of the {ids.Count} documents.")
                .WithMetadata("ids", ids).CausedBy(e));
        }
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.GetOrCreateClientAsync(type).ConfigureAwait(false);
            var response = await client.SendAsync(HttpMethod.Delete,
                $"collections/{TypesenseHttpClientWrapper.Escape(type)}/documents/{TypesenseHttpClientWrapper.Escape(id)}", null, cancellationToken).ConfigureAwait(false);
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
        var batches = distinctIds.Value.Chunk(RemoveBatchSize).ToList();
        for (var i = 0; i < batches.Count; i++)
        {
            var remaining = batches.Skip(i).SelectMany(batch => batch).ToList();
            try
            {
                var client = await clientFactory.GetOrCreateClientAsync(index).ConfigureAwait(false);
                var filter = Uri.EscapeDataString($"id:[{string.Join(',', batches[i].Select(id => $"`{id}`"))}]");
                var response = await client.SendAsync(HttpMethod.Delete,
                    $"collections/{TypesenseHttpClientWrapper.Escape(index)}/documents?filter_by={filter}", null, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccess)
                {
                    return Result.Fail<int>(DocumentIds.FailedError(index, removed, distinctIds.Value.Count, remaining, response.Describe()));
                }

                removed += DeletedCount(response);
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

    private static int DeletedCount(TypesenseResponse response)
    {
        using var json = JsonDocument.Parse(response.Body);
        return json.RootElement.TryGetProperty("num_deleted", out var deleted) ? deleted.GetInt32() : 0;
    }
}
