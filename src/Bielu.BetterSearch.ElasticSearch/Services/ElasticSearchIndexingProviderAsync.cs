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
    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var indexResult = await (await clientFactory.GetOrCreateClientAsync(document.Index)).IndexAsync(document,
            cancellationToken);
        return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
    }

    public async Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        //implement bulk indexing
        var searchDocuments = documents.ToList();
        var results = new List<Result>();
        foreach (var indexGroup in searchDocuments.GroupBy(x=>x.Index))
        {
            var client = await clientFactory.GetOrCreateClientAsync(indexGroup.Key);
            var bulkResponse = await client.BulkAsync(b => b.IndexMany(indexGroup.Select(x=>x), (descriptor, _) => descriptor.Index(indexGroup.Key)), cancellationToken);
            results.Add(bulkResponse.IsSuccess() ? Result.Ok() : Result.Fail(new Error(bulkResponse.DebugInformation)));
        }

        return results.Merge();
    }

    public async Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        var indexResult = await (await clientFactory.GetOrCreateClientAsync(type)).DeleteAsync(new DeleteRequest(type, id), cancellationToken);
        return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
    }

    public async Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest index,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteByQueryRequest(Indices.Index(index.IndexName));
        request.Query = new MatchAllQuery();
        var indexResult = await (await clientFactory.GetOrCreateClientAsync(index.IndexName)).DeleteByQueryAsync(request,
            cancellationToken);
        return indexResult.IsSuccess() ? Result.Ok(indexResult.Deleted) : Result.Fail(new Error(indexResult.DebugInformation));
    }

    public async Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        var exist = await IndexExistsAsync(index, cancellationToken);
        if (exist is { IsSuccess: true, Value: true })
        {
            return Result.Ok();
        }

        return await CreateIndexAsync(index, cancellationToken);
    }

    public async Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var indexResult = await (await clientFactory.GetOrCreateClientAsync(index)).Indices.DeleteAsync(new DeleteIndexRequest(Indices.Index(index)), cancellationToken);
        return indexResult.IsSuccess() ? Result.Ok() : Result.Fail(new Error(indexResult.DebugInformation));
    }

    public async Task<Result<bool>> IndexExistsAsync(string index, CancellationToken cancellationToken = default)
    {
        try
        {
            var result= await (await clientFactory.GetOrCreateClientAsync(index)).Indices.ExistsAsync(new ExistsRequest(Indices.Index(index)), cancellationToken);
            return result.Exists;
        }
        catch (Exception e)
        {
            return Result.Fail(new Error(e.Message));
        }

    }

    public async Task<Result<bool>> CreateIndexAsync(string index, CancellationToken cancellationToken = default)  {
        try
        {
            var result= await (await clientFactory.GetOrCreateClientAsync(index)).Indices.CreateAsync(new CreateIndexRequest(Indices.Index(index)), cancellationToken);
            if (!result.IsSuccess())
            {
                logger.LogError("Indexing failed: {Error}", result.DebugInformation);
            }
            return result.IsSuccess();
        }
        catch (Exception e)
        {
            return Result.Fail(new Error(e.Message));
        }
    }

}
