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
    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(document.Index, async client =>
        {
            var indexResult = await client.IndexAsync(StoredFields.WithDatesAsText(document), document.Index, document.Id, cancellationToken).ConfigureAwait(false);
            return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
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
                var request = new BulkRequest(indexGroup.Key)
                {
                    Operations = new BulkOperationsCollection(group.Select(document =>
                        new BulkIndexOperation<SearchDocument>(StoredFields.WithDatesAsText(document)) { Id = document.Id })),
                };
                var response = await client.BulkAsync(request, cancellationToken).ConfigureAwait(false);
                // A request rejected as a whole, such as a 403, has no items.
                if (response.Items is null || response.Items.Count != group.Count)
                {
                    return Result.Fail<int>(RequestFailed(ElasticSearchFailures.Describe("bulk index", response), response.ApiCallDetails?.OriginalException));
                }

                // Elasticsearch applies each item on its own, so the documents it accepted stay stored.
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
            var indexResult = await client.DeleteAsync(new DeleteRequest(type, id), cancellationToken).ConfigureAwait(false);
            // Deletes are idempotent: a document that is not there is already gone. A missing index is still a failure,
            // and Elasticsearch reports it with an error rather than a not_found result.
            var removed = indexResult.ElasticsearchServerError is null
                && (indexResult.IsSuccess() || indexResult.Result == Elastic.Clients.Elasticsearch.Result.NotFound);
            return removed ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
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
            var request = new BulkRequest(index)
            {
                Operations = new BulkOperationsCollection(distinctIds.Value.Select(id => new BulkDeleteOperation(id))),
            };
            var response = await client.BulkAsync(request, cancellationToken).ConfigureAwait(false);
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync<long?>(index.IndexName, async client =>
        {
            var request = new DeleteByQueryRequest(Indices.Index(index.IndexName));
            request.Query = new MatchAllQuery();
            var indexResult = await client.DeleteByQueryAsync(request, cancellationToken).ConfigureAwait(false);
            return indexResult.IsSuccess() ? Result.Ok(indexResult.Deleted) : Result.Fail(new Error(indexResult.DebugInformation));
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
            var indexResult = await client.Indices.DeleteAsync(new DeleteIndexRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
            return indexResult.IsSuccess() ? Result.Ok(true) : Result.Fail<bool>(new Error(indexResult.DebugInformation));
        }, cancellationToken);

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var result = await client.Indices.ExistsAsync(new ExistsRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            // Only 200 and 404 answer the question; anything else, such as no answer, a 401, 403 or 5xx, is a failure.
            return result.ApiCallDetails?.HttpStatusCode switch
            {
                200 => Result.Ok(true),
                404 => Result.Ok(false),
                _ => Result.Fail<bool>(RequestFailed(ElasticSearchFailures.Describe("index exists check", result), result.ApiCallDetails?.OriginalException)),
            };
        }, cancellationToken);

    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var result = await client.Indices.CreateAsync(new CreateIndexRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsSuccess())
            {
                return Result.Ok(true);
            }

            // An index that already exists was not created, but nothing went wrong.
            if (result.ElasticsearchServerError?.Error?.Type == "resource_already_exists_exception")
            {
                return Result.Ok(false);
            }

            logger.LogError("Creating index {Index} failed: {Error}", index, result.DebugInformation);
            return Result.Fail<bool>(RequestFailed(ElasticSearchFailures.Describe("create index", result), result.ApiCallDetails?.OriginalException));
        }, cancellationToken);

    private static Error RequestFailed(string message, Exception? exception)
    {
        var error = new Error(message);
        return exception is null ? error : error.CausedBy(exception);
    }
}
