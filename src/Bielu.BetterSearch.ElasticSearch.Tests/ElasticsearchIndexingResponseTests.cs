using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.ElasticSearch.Configuration;
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

    private static readonly IElasticSearchClientSettingsManager NoMappings =
        new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(new ElasticSearchOptions { SharedIndexSettings = new() }));

    private static readonly IElasticSearchClientSettingsManager WithMappings =
        new ElasticSearchClientSettingsManager(new FixedOptionsMonitor(new ElasticSearchOptions
        {
            SharedIndexSettings = new() { Mappings = [new() { Field = "City", Type = ElasticSearchFieldType.Keyword }] }
        }));

    /// <summary>Answers HEAD requests, such as the index exists check, with one invoker and every other request with another.</summary>
    private sealed class HeadRoutingRequestInvokerDecorator(IRequestInvoker head, IRequestInvoker other) : IRequestInvoker
    {
        public ResponseFactory ResponseFactory => other.ResponseFactory;

        private IRequestInvoker For(Endpoint endpoint) => endpoint.Method == Elastic.Transport.HttpMethod.HEAD ? head : other;

        public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
            where TResponse : TransportResponse, new() =>
            For(endpoint).Request<TResponse>(endpoint, boundConfiguration, postData);

        public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, CancellationToken cancellationToken)
            where TResponse : TransportResponse, new() =>
            For(endpoint).RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken);

        public void Dispose()
        {
        }
    }

    /// <summary>Answers each request with the next invoker in turn, and counts the requests made.</summary>
    private sealed class SequenceRequestInvoker(params IRequestInvoker[] invokers) : IRequestInvoker
    {
        public int Requests { get; private set; }

        public ResponseFactory ResponseFactory => invokers[0].ResponseFactory;

        private IRequestInvoker Next() => invokers[Requests++];

        public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
            where TResponse : TransportResponse, new() =>
            Next().Request<TResponse>(endpoint, boundConfiguration, postData);

        public Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, CancellationToken cancellationToken)
            where TResponse : TransportResponse, new() =>
            Next().RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken);

        public void Dispose()
        {
        }
    }

    private static InMemoryRequestInvoker Invoker(string json, int statusCode) =>
        new(Encoding.UTF8.GetBytes(json), statusCode, null, "application/json",
            new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = ["Elasticsearch"] });

    private static ElasticSearchIndexingProviderAsync Provider(string json, int statusCode, bool throwExceptions = false,
        IElasticSearchClientSettingsManager? settingsManager = null) =>
        Provider(Invoker(json, statusCode), throwExceptions, settingsManager);

    private static ElasticSearchIndexingProviderAsync Provider(IRequestInvoker invoker, bool throwExceptions = false,
        IElasticSearchClientSettingsManager? settingsManager = null)
    {
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:9200")), invoker)
            .ThrowExceptions(throwExceptions);
        return new ElasticSearchIndexingProviderAsync(new FixedClientFactory(new ElasticsearchClient(settings)), NullLogger<ElasticSearchIndexingProviderAsync>.Instance, settingsManager ?? NoMappings);
    }

    private const string AlreadyExists = """{"error":{"root_cause":[{"type":"resource_already_exists_exception","reason":"index [people/abc] already exists","index":"people"}],"type":"resource_already_exists_exception","reason":"index [people/abc] already exists","index":"people"},"status":400}""";

    private const string CreateForbidden = """{"error":{"root_cause":[{"type":"security_exception","reason":"no permissions for [indices:admin/create]"}],"type":"security_exception","reason":"no permissions for [indices:admin/create]"},"status":403}""";

    /// <summary>A provider that talks to <paramref name="port"/> on this machine over the real HTTP transport.</summary>
    private static ElasticSearchIndexingProviderAsync LocalProvider(int port)
    {
        var settings = new ElasticsearchClientSettings(new Uri($"http://127.0.0.1:{port}"));
        return new ElasticSearchIndexingProviderAsync(new FixedClientFactory(new ElasticsearchClient(settings)), NullLogger<ElasticSearchIndexingProviderAsync>.Instance, NoMappings);
    }

    /// <summary>A port nothing listens on, so a connection to it is refused.</summary>
    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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

    [Fact]
    public async Task RemoveDocumentsAsync_OneItemFails_ReturnsFailureNamingOnlyThatId()
    {
        var provider = Provider("""{"took":5,"errors":true,"items":[{"delete":{"_index":"people","_id":"1","_version":2,"result":"deleted","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":1,"_primary_term":1,"status":200}},{"delete":{"_index":"people","_id":"2","_version":1,"result":"not_found","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":2,"_primary_term":1,"status":404}},{"delete":{"_index":"people","_id":"3","status":409,"error":{"type":"version_conflict_engine_exception","reason":"[3]: version conflict","index":"people"}}}]}""", 200);

        var result = await provider.RemoveDocumentsAsync("people", ["1", "2", "3"], Token);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldBe("Removing documents from people failed for ids 3: [3]: version conflict. 1 of 3 documents were removed.");
        error.Metadata["ids"].ShouldBe(new List<string> { "3" });
    }

    [Fact]
    public async Task RemoveDocumentsAsync_DeletedAndNotFound_CountsOnlyTheDeleted()
    {
        var provider = Provider("""{"took":5,"errors":false,"items":[{"delete":{"_index":"people","_id":"1","_version":2,"result":"deleted","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":1,"_primary_term":1,"status":200}},{"delete":{"_index":"people","_id":"2","_version":1,"result":"not_found","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":2,"_primary_term":1,"status":404}}]}""", 200);

        var result = await provider.RemoveDocumentsAsync("people", ["1", "2"], Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveDocumentsAsync_RequestRejected_ReturnsFailure()
    {
        var provider = Provider("""{"error":{"type":"security_exception","reason":"no permissions"},"status":403}""", 403);

        var result = await provider.RemoveDocumentsAsync("people", ["1", "2"], Token);

        var message = result.Errors.ShouldHaveSingleItem().Message;
        message.ShouldBe("Elasticsearch bulk delete failed with HTTP 403 (security_exception): no permissions");
    }

    private const string DeletedTwo = """{"took":5,"timed_out":false,"total":2,"deleted":2,"batches":1,"version_conflicts":0,"noops":0,"failures":[]}""";

    [Fact]
    public async Task RemoveAllDocumentsAsync_RefreshForbidden_ReturnsTheRefreshFailureWithoutDeleting()
    {
        var invoker = new SequenceRequestInvoker(
            Invoker("""{"error":{"type":"security_exception","reason":"no permissions for [indices:admin/refresh]"},"status":403}""", 403),
            Invoker(DeletedTwo, 200));
        var provider = Provider(invoker);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Elasticsearch refresh of index people before removing all documents failed with HTTP 403 (security_exception): no permissions for [indices:admin/refresh]");
        invoker.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_RefreshFailsOnAShard_ReturnsFailureWithoutDeleting()
    {
        var invoker = new SequenceRequestInvoker(
            Invoker("""{"_shards":{"total":2,"successful":1,"failed":1}}""", 200),
            Invoker(DeletedTwo, 200));
        var provider = Provider(invoker);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Refreshing index people before removing all documents failed on 1 of 2 shards, so no documents were removed.");
        invoker.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_Refreshed_ReturnsTheDeletedCount()
    {
        var invoker = new SequenceRequestInvoker(
            Invoker("""{"_shards":{"total":2,"successful":1,"failed":0}}""", 200),
            Invoker(DeletedTwo, 200));
        var provider = Provider(invoker);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        invoker.Requests.ShouldBe(2);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(404, false)]
    public async Task IndexExistsAsync_ServerAnswers_ReturnsWhetherTheIndexExists(int statusCode, bool expected)
    {
        var provider = Provider("{}", statusCode);

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(503)]
    public async Task IndexExistsAsync_RequestFails_ReturnsFailure(int statusCode)
    {
        var provider = Provider("{}", statusCode);

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldStartWith("Elasticsearch index exists check failed");
        result.Errors[0].Message.ShouldContain(statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        result.Errors[0].Message.ShouldNotContain("Audit trail");
    }

    [Fact]
    public async Task IndexExistsAsync_ConnectionRefused_ReturnsFailureCausedByTheTransportException()
    {
        // With ThrowExceptions off, which is how the provider's clients are built, there is no status code and the
        // transport's exception is only on the response.
        var provider = LocalProvider(ClosedPort());

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldStartWith("Elasticsearch index exists check failed");
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ExistsCheckFails_ReturnsTheCheckFailure()
    {
        var provider = Provider("{}", 401);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldStartWith("Elasticsearch index exists check failed with HTTP 401");
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_IndexExists_ReturnsTrue()
    {
        var provider = Provider("{}", 200);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_IndexCreated_ReturnsTrue()
    {
        var provider = Provider(new HeadRoutingRequestInvokerDecorator(Invoker("{}", 404), Invoker("""{"acknowledged":true,"shards_acknowledged":true,"index":"people"}""", 200)));

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_CreatedByAnotherCallerFirst_ReturnsTrue()
    {
        // The exists check sees no index, then another caller creates it before this caller's create.
        var provider = Provider(new HeadRoutingRequestInvokerDecorator(Invoker("{}", 404), Invoker(AlreadyExists, 400)));

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_CreatedByAnotherCallerFirstWithMappings_AddsThemAndReturnsTrue()
    {
        // The exists check sees no index, another caller creates it before this caller's create, so the mappings are put.
        var invoker = new SequenceRequestInvoker(Invoker("{}", 404), Invoker(AlreadyExists, 400), Invoker("""{"acknowledged":true}""", 200));
        var provider = Provider(invoker, settingsManager: WithMappings);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        invoker.Requests.ShouldBe(3);
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_CreateForbidden_ReturnsTheCreateFailure()
    {
        var provider = Provider(new HeadRoutingRequestInvokerDecorator(Invoker("{}", 404), Invoker(CreateForbidden, 403)));

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("Elasticsearch create index failed with HTTP 403 (security_exception): no permissions for [indices:admin/create]");
    }

    [Fact]
    public async Task CreateIndexAsync_Created_ReturnsTrue()
    {
        var provider = Provider("""{"acknowledged":true,"shards_acknowledged":true,"index":"people"}""", 200);

        var result = await provider.CreateIndexAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateIndexAsync_AlreadyExists_ReturnsFalse()
    {
        var provider = Provider(AlreadyExists, 400);

        var result = await provider.CreateIndexAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateIndexAsync_Forbidden_ReturnsFailure()
    {
        var provider = Provider(CreateForbidden, 403);

        var result = await provider.CreateIndexAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("Elasticsearch create index failed with HTTP 403 (security_exception): no permissions for [indices:admin/create]");
    }

    [Fact]
    public async Task DeleteIndexAsync_Deleted_ReturnsTrue()
    {
        var provider = Provider("""{"acknowledged":true}""", 200);

        var result = await provider.DeleteIndexAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteIndexAsync_IndexNotFound_ReturnsFailure()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [people]"}],"type":"index_not_found_exception","reason":"no such index [people]"},"status":404}""", 404);

        var result = await provider.DeleteIndexAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("index_not_found_exception");
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_MappingsNotAcknowledged_ReturnsFailureSayingTheyMayStillApply()
    {
        // The in-memory invoker answers every request with this body: the exists check with 200, then the put-mapping.
        var provider = Provider("""{"acknowledged":false}""", 200, settingsManager: WithMappings);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldBe("Adding the configured mappings to index people was not acknowledged in time. The mappings are not rolled back and may still apply; call EnsureIndexExistsAsync again to confirm them.");
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_MappingsAcknowledged_ReturnsTrueForAnExistingIndex()
    {
        var provider = Provider("""{"acknowledged":true}""", 200, settingsManager: WithMappings);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    public static TheoryData<string> IndexingMethods =>
    [
        nameof(IIndexingProviderAsync.IndexDocumentAsync),
        nameof(IIndexingProviderAsync.IndexMultipleDocumentsAsync),
        nameof(IIndexingProviderAsync.RemoveDocumentAsync),
        nameof(IIndexingProviderAsync.RemoveDocumentsAsync),
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
            nameof(IIndexingProviderAsync.RemoveDocumentsAsync) => await provider.RemoveDocumentsAsync("people", ["1", "2"], cancellationToken),
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
        var provider = new ElasticSearchIndexingProviderAsync(new ThrowingClientFactory(exception), NullLogger<ElasticSearchIndexingProviderAsync>.Instance, NoMappings);

        var result = await InvokeAsync(provider, method, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["bad connection string"]);
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_RequestThrows_ReturnsFailureCausedByTheException(string method)
    {
        // With ThrowExceptions on, a failed request throws a TransportException rather than returning a response.
        var provider = Provider("""{"error":"unavailable","status":503}""", 503, throwExceptions: true);

        var result = await InvokeAsync(provider, method, Token);

        result.IsFailed.ShouldBeTrue();
        var exception = result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeOfType<TransportException>();
        result.Errors.Select(e => e.Message).ShouldBe([exception.Message]);
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_CancelledMidRequest_ThrowsOperationCanceledException(string method)
    {
        // The server accepts the connection and never answers, so the request is still in flight when it is cancelled.
        // With ThrowExceptions off, the client reports that as a response with no status code rather than throwing.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var provider = LocalProvider(((IPEndPoint)listener.LocalEndpoint).Port);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

            await Should.ThrowAsync<OperationCanceledException>(() => InvokeAsync(provider, method, cancellation.Token));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_ClientFactoryCancelled_ThrowsOperationCanceledException(string method)
    {
        var provider = new ElasticSearchIndexingProviderAsync(new ThrowingClientFactory(new OperationCanceledException()), NullLogger<ElasticSearchIndexingProviderAsync>.Instance, NoMappings);

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
