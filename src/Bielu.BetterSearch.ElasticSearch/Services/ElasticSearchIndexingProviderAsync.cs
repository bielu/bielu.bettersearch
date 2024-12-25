using Bielu.BetterSearch.Abstractions.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using FluentResults;
using Microsoft.Extensions.Logging;
using ExistsRequest = Elastic.Clients.Elasticsearch.IndexManagement.ExistsRequest;
using Result = FluentResults.Result;

namespace Bielu.BetterSearch.Lifti.Services;

public class ElasticSearchIndexingProviderAsync(IClientFactoryAsync<ElasticsearchClient> clientFactory, ILogger<ElasticSearchIndexingProviderAsync> logger) : IIndexingProviderAsync
{
    public async Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default) => await (await clientFactory.GetOrCreateClientAsync(document.Index)).IndexAsync(document, cancellationToken);
    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> document, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result> RemoveDocumentAsync(string id, string type, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<int>> RemoveAllDocumentsAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> EnsureIndexExistsAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Result<bool>> DeleteIndexAsync(string index, CancellationToken cancellationToken = default) => throw new NotImplementedException();

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


    Task<Result> IIndexingProviderAsync.IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken) => throw new NotImplementedException();
}
