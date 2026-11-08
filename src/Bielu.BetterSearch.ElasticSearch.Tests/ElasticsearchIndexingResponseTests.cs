using System.Text;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>
/// Responses a healthy test cluster does not produce, served from an in-memory request invoker.
/// </summary>
public sealed class ElasticsearchIndexingResponseTests
{
    private sealed class FixedClientFactory(ElasticsearchClient client) : IClientFactoryAsync<ElasticsearchClient>
    {
        public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private sealed class ThrowingClientFactory(Exception exception) : IClientFactoryAsync<ElasticsearchClient>
    {
        public Task<ElasticsearchClient> GetOrCreateClientAsync(string indexName) => throw exception;
    }

    private static ElasticSearchIndexingProviderAsync Provider(string json, int statusCode)
    {
        var invoker = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(json), statusCode, null, "application/json",
            new Dictionary<string, IEnumerable<string>> { ["X-Elastic-Product"] = ["Elasticsearch"] });
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:9200")), invoker);
        return new ElasticSearchIndexingProviderAsync(new FixedClientFactory(new ElasticsearchClient(settings)), NullLogger<ElasticSearchIndexingProviderAsync>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemoveDocumentAsync_DocumentNotFound_ReturnsSuccess()
    {
        var provider = Provider("""{"_index":"people","_id":"1","_version":1,"result":"not_found","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":0,"_primary_term":1}""", 404);

        var result = await provider.RemoveDocumentAsync("1", "people", Token);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveDocumentAsync_IndexNotFound_ReturnsFailure()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [people]"}],"type":"index_not_found_exception","reason":"no such index [people]"},"status":404}""", 404);

        var result = await provider.RemoveDocumentAsync("1", "people", Token);

        result.IsFailed.ShouldBeTrue();
    }

    public static TheoryData<string> IndexingMethods =>
    [
        nameof(IIndexingProviderAsync.IndexDocumentAsync),
        nameof(IIndexingProviderAsync.IndexMultipleDocumentsAsync),
        nameof(IIndexingProviderAsync.RemoveDocumentAsync),
        nameof(IIndexingProviderAsync.RemoveAllDocumentsAsync),
        nameof(IIndexingProviderAsync.EnsureIndexExistsAsync),
        nameof(IIndexingProviderAsync.DeleteIndexAsync),
        nameof(IIndexingProviderAsync.IndexExistsAsync),
        nameof(IIndexingProviderAsync.CreateIndexAsync),
    ];

    private static async Task<IResultBase> InvokeAsync(ElasticSearchIndexingProviderAsync provider, string method, CancellationToken cancellationToken)
    {
        var document = new SearchDocument { Id = "1", Type = "TestDocument", Index = "people" };
        return method switch
        {
            nameof(IIndexingProviderAsync.IndexDocumentAsync) => await provider.IndexDocumentAsync(document, cancellationToken),
            nameof(IIndexingProviderAsync.IndexMultipleDocumentsAsync) => await provider.IndexMultipleDocumentsAsync([document], cancellationToken),
            nameof(IIndexingProviderAsync.RemoveDocumentAsync) => await provider.RemoveDocumentAsync("1", "people", cancellationToken),
            nameof(IIndexingProviderAsync.RemoveAllDocumentsAsync) => await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, cancellationToken),
            nameof(IIndexingProviderAsync.EnsureIndexExistsAsync) => await provider.EnsureIndexExistsAsync("people", cancellationToken),
            nameof(IIndexingProviderAsync.DeleteIndexAsync) => await provider.DeleteIndexAsync("people", cancellationToken),
            nameof(IIndexingProviderAsync.IndexExistsAsync) => await provider.IndexExistsAsync("people", cancellationToken),
            nameof(IIndexingProviderAsync.CreateIndexAsync) => await provider.CreateIndexAsync("people", cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
        };
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_ClientFactoryThrows_ReturnsFailureCausedByTheException(string method)
    {
        var exception = new InvalidOperationException("bad connection string");
        var provider = new ElasticSearchIndexingProviderAsync(new ThrowingClientFactory(exception), NullLogger<ElasticSearchIndexingProviderAsync>.Instance);

        var result = await InvokeAsync(provider, method, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["bad connection string"]);
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_ClientFactoryCancelled_ThrowsOperationCanceledException(string method)
    {
        var provider = new ElasticSearchIndexingProviderAsync(new ThrowingClientFactory(new OperationCanceledException()), NullLogger<ElasticSearchIndexingProviderAsync>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => InvokeAsync(provider, method, Token));
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_CancelledToken_ThrowsOperationCanceledException(string method)
    {
        var provider = Provider("{}", 200);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => InvokeAsync(provider, method, cancellation.Token));
    }
}
