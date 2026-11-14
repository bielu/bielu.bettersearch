using Bielu.BetterSearch.Abstractions.Configuration;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentResults;
using Microsoft.Extensions.Logging;
using ExistsRequest = Elastic.Clients.Elasticsearch.IndexManagement.ExistsRequest;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.ElasticSearch.Services;

public class ElasticSearchIndexingProviderAsync(
    IClientFactoryAsync<ElasticsearchClient> clientFactory,
    ILogger<ElasticSearchIndexingProviderAsync> logger,
    IElasticSearchClientSettingsManager settingsManager) : IIndexingProviderAsync
{
    public Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(document.Index, async client =>
        {
            var request = new IndexRequest<SearchDocument>(StoredFields.WithDatesAsText(document), document.Index, document.Id) { Refresh = WaitFor(document.Index) };
            var indexResult = await client.IndexAsync(request, cancellationToken).ConfigureAwait(false);
            return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
        }, cancellationToken);

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        //implement bulk indexing
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x => x.Index))
        {
            results.Add(await clientFactory.TryWithClientAsync(indexGroup.Key, async client =>
            {
                var bulkResponse = await client.BulkAsync(b => b.IndexMany(indexGroup.Select(StoredFields.WithDatesAsText), (descriptor, _) => descriptor.Index(indexGroup.Key)).Refresh(WaitFor(indexGroup.Key)), cancellationToken).ConfigureAwait(false);
                return bulkResponse.IsSuccess() ? Result.Ok() : Result.Fail(new Error(bulkResponse.DebugInformation));
            }, cancellationToken).ConfigureAwait(false));
        }

        return results.Merge();
    }

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(type, async client =>
        {
            var indexResult = await client.DeleteAsync(new DeleteRequest(type, id) { Refresh = WaitFor(type) }, cancellationToken).ConfigureAwait(false);
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
                Refresh = WaitFor(index),
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
            // Delete by query can't wait for a refresh, so it refreshes the index itself.
            request.Refresh = WaitsUntilSearchable(index.IndexName) ? true : null;
            var indexResult = await client.DeleteByQueryAsync(request, cancellationToken).ConfigureAwait(false);
            return indexResult.IsSuccess() ? Result.Ok(indexResult.Deleted) : Result.Fail(new Error(indexResult.DebugInformation));
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
            var properties = ElasticSearchMappings.Create(settingsManager.GetConnectionSettings(index).Mappings);
            if (properties is null)
            {
                return Result.Ok(false);
            }

            var response = await client.Indices.PutMappingAsync(new PutMappingRequest(Indices.Index(index)) { Properties = properties }, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess())
            {
                return Result.Fail<bool>(RequestFailed(ElasticSearchFailures.Describe($"adding the configured mappings to index {index}", response), response.ApiCallDetails?.OriginalException));
            }

            // A 200 with acknowledged false means the cluster took the mappings but did not confirm them in time.
            return response.Acknowledged
                ? Result.Ok(true)
                : Result.Fail<bool>(NotAcknowledged(index));
        }, cancellationToken);

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

    /// <summary>
    /// Creates the index with its configured mappings. <c>false</c> when it already exists, which leaves its mappings as
    /// they are; any other refusal, such as an unknown analyzer, is a failure.
    /// </summary>
    public Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var request = new CreateIndexRequest(Indices.Index(index));
            var properties = ElasticSearchMappings.Create(settingsManager.GetConnectionSettings(index).Mappings);
            if (properties is not null)
            {
                request.Mappings = new TypeMapping { Properties = properties };
            }

            var result = await client.Indices.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a failed response; check before logging it as an error.
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

    private bool WaitsUntilSearchable(string index) =>
        settingsManager.GetConnectionSettings(index).WriteVisibility == WriteVisibility.WaitUntilSearchable;

    /// <summary><c>refresh=wait_for</c> when the index's writes wait until they are searchable; otherwise the engine's default.</summary>
    private Refresh? WaitFor(string index) => WaitsUntilSearchable(index) ? Refresh.WaitFor : null;

    private static string NotAcknowledged(string index) =>
        $"Adding the configured mappings to index {index} was not acknowledged in time. The mappings are not rolled back and may still apply; call EnsureIndexExistsAsync again to confirm them.";

    private static Error RequestFailed(string message, Exception? exception)
    {
        var error = new Error(message);
        return exception is null ? error : error.CausedBy(exception);
    }
}
