using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using Microsoft.Extensions.Logging;
using ExistsRequest = Elastic.Clients.Elasticsearch.IndexManagement.ExistsRequest;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchIndexingProviderAsync(IClientFactoryAsync<ElasticsearchClient> clientFactory, ILogger<ElasticSearchIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(document.Index, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult();
        }

        var indexResult = await client.Value.IndexAsync(document, document.Index, document.Id, cancellationToken).ConfigureAwait(false);
        return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        //implement bulk indexing
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            var client = await clientFactory.TryGetClientAsync(indexGroup.Key, cancellationToken).ConfigureAwait(false);
            if (client.IsFailed)
            {
                results.Add(client.ToResult());
                continue;
            }

            var bulkResponse = await client.Value.BulkAsync(b => b.IndexMany(indexGroup.Select(x => x), (descriptor, _) => descriptor.Index(indexGroup.Key)), cancellationToken).ConfigureAwait(false);
            results.Add(bulkResponse.IsSuccess() ? Result.Ok() : Result.Fail(new Error(bulkResponse.DebugInformation)));
        }

        return results.Merge();
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(type, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult();
        }

        try
        {
            var indexResult = await client.Value.DeleteAsync(new DeleteRequest(type, id), cancellationToken).ConfigureAwait(false);
            // Deletes are idempotent: a document that is not there is already gone. A missing index is still a failure,
            // and Elasticsearch reports it with an error rather than a not_found result.
            var removed = indexResult.ElasticsearchServerError is null
                && (indexResult.IsSuccess() || indexResult.Result == Elastic.Clients.Elasticsearch.Result.NotFound);
            return removed ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
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

        try
        {
            var client = await clientFactory.TryGetClientAsync(index, cancellationToken).ConfigureAwait(false);
            if (client.IsFailed)
            {
                return client.ToResult<int>();
            }

            var request = new BulkRequest(index)
            {
                Operations = new BulkOperationsCollection(distinctIds.Value.Select(id => new BulkDeleteOperation(id))),
            };
            var response = await client.Value.BulkAsync(request, cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
            // A request rejected as a whole, such as a 403, has no items.
            if (response.Items is null || response.Items.Count != distinctIds.Value.Count)
            {
                return Result.Fail<int>(RequestFailed(ElasticSearchFailures.Describe("bulk delete", response), response.ApiCallDetails?.OriginalException));
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
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail<int>(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(index.IndexName, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<long?>();
        }

        var request = new DeleteByQueryRequest(Indices.Index(index.IndexName));
        request.Query = new MatchAllQuery();
        var indexResult = await client.Value.DeleteByQueryAsync(request, cancellationToken).ConfigureAwait(false);
        return indexResult.IsSuccess() ? Result.Ok(indexResult.Deleted) : Result.Fail(new Error(indexResult.DebugInformation));
    }

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
            return Result.Ok();
        }

        return await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(index, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<bool>();
        }

        var indexResult = await client.Value.Indices.DeleteAsync(new DeleteIndexRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
        return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await clientFactory.TryGetClientAsync(index, cancellationToken).ConfigureAwait(false);
            if (client.IsFailed)
            {
                return client.ToResult<bool>();
            }

            var result = await client.Value.Indices.ExistsAsync(new ExistsRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
            // Only 200 and 404 answer the question; anything else, such as no answer, a 401, 403 or 5xx, is a failure.
            return result.ApiCallDetails?.HttpStatusCode switch
            {
                200 => Result.Ok(true),
                404 => Result.Ok(false),
                _ => Result.Fail<bool>(RequestFailed(ElasticSearchFailures.Describe("index exists check", result), result.ApiCallDetails?.OriginalException)),
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }
    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryGetClientAsync(index, cancellationToken).ConfigureAwait(false);
        if (client.IsFailed)
        {
            return client.ToResult<bool>();
        }

        try
        {
            var result = await client.Value.Indices.CreateAsync(new CreateIndexRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess())
            {
                logger.LogError("Indexing failed: {Error}", result.DebugInformation);
            }
            return result.IsSuccess();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result.Fail(new Error(e.Message).CausedBy(e));
        }
    }

    private static Error RequestFailed(string message, Exception? exception)
    {
        var error = new Error(message);
        return exception is null ? error : error.CausedBy(exception);
    }
}
