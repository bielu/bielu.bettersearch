using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
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
            var indexResult = await client.IndexAsync(document, document.Index, document.Id, cancellationToken).ConfigureAwait(false);
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
                var bulkResponse = await client.BulkAsync(b => b.IndexMany(indexGroup.Select(x => x), (descriptor, _) => descriptor.Index(indexGroup.Key)), cancellationToken).ConfigureAwait(false);
                return bulkResponse.IsSuccess() ? Result.Ok() : Result.Fail(new Error(bulkResponse.DebugInformation));
            }, cancellationToken).ConfigureAwait(false));
        }

        return results.Merge();
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
            return Result.Ok();
        }

        return await CreateIndexAsync(index, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync<bool>(index, async client =>
        {
            var indexResult = await client.Indices.DeleteAsync(new DeleteIndexRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
        }, cancellationToken);

    public Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default) =>
        clientFactory.TryWithClientAsync(index, async client =>
        {
            var result = await client.Indices.ExistsAsync(new ExistsRequest(Indices.Index(index)), cancellationToken).ConfigureAwait(false);
            // The client reports a cancelled request as a response with no status code; the caller cancelled, so say that instead.
            cancellationToken.ThrowIfCancellationRequested();
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
            if (!result.IsSuccess())
            {
                logger.LogError("Indexing failed: {Error}", result.DebugInformation);
            }
            return Result.Ok(result.IsSuccess());
        }, cancellationToken);

    private static Error RequestFailed(string message, Exception? exception)
    {
        var error = new Error(message);
        return exception is null ? error : error.CausedBy(exception);
    }
}
