using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Bielu.BetterSearch.Services;
using Bielu.BetterSearch.Tests;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>
/// Responses a healthy test cluster does not produce, served from an in-memory connection.
/// </summary>
public sealed class OpenSearchIndexingResponseTests : RemoveDocumentResponseTestBase
{
    private sealed class FixedClientFactory(IOpenSearchClient client) : IClientFactoryAsync<IOpenSearchClient>
    {
        public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private sealed class CancelledClientFactory : IClientFactoryAsync<IOpenSearchClient>
    {
        public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName) => throw new OperationCanceledException();
    }

    private sealed class ThrowingClientFactory(Exception exception) : IClientFactoryAsync<IOpenSearchClient>
    {
        public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName) => throw exception;
    }

    private static readonly IOpenSearchClientSettingsManager NoMappings =
        new OpenSearchClientSettingsManager(InvalidSettingsOptions());

    /// <summary>Options whose connection string has no scheme, new on every call so a test can change them.</summary>
    private static TestOptionsMonitor<OpenSearchOptions> InvalidSettingsOptions(int maxResultWindow = 10_000) =>
        new(new OpenSearchOptions { SharedIndexSettings = new() { ConnectionString = "localhost:9200" }, MaxResultWindow = maxResultWindow });

    private static IClientFactoryAsync<IOpenSearchClient> InvalidSettingsFactory() =>
        new OpenSearchClientFactory(new OpenSearchClientSettingsManager(InvalidSettingsOptions()));

    private static IOpenSearchClientSettingsManager WithMappings()
    {
        var monitor = InvalidSettingsOptions();
        monitor.CurrentValue.SharedIndexSettings.Mappings = [new() { Field = "City", Type = OpenSearchFieldType.Keyword }];
        return new OpenSearchClientSettingsManager(monitor);
    }

    /// <summary>Answers HEAD requests, such as the index exists check, with one connection and every other request with another.</summary>
    private sealed class HeadRoutingConnectionDecorator(IConnection head, IConnection other) : IConnection
    {
        private IConnection For(RequestData requestData) => requestData.Method == global::OpenSearch.Net.HttpMethod.HEAD ? head : other;

        public TResponse Request<TResponse>(RequestData requestData)
            where TResponse : class, IOpenSearchResponse, new() =>
            For(requestData).Request<TResponse>(requestData);

        public Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
            where TResponse : class, IOpenSearchResponse, new() =>
            For(requestData).RequestAsync<TResponse>(requestData, cancellationToken);

        public void Dispose()
        {
        }
    }

    /// <summary>Answers each request with the next connection in turn, and counts the requests made.</summary>
    private sealed class SequenceConnection(params IConnection[] connections) : IConnection
    {
        public int Requests { get; private set; }

        private IConnection Next() => connections[Requests++];

        public TResponse Request<TResponse>(RequestData requestData)
            where TResponse : class, IOpenSearchResponse, new() =>
            Next().Request<TResponse>(requestData);

        public Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
            where TResponse : class, IOpenSearchResponse, new() =>
            Next().RequestAsync<TResponse>(requestData, cancellationToken);

        public void Dispose()
        {
        }
    }

    protected override IIndexingProviderAsync ProviderAnswering(string json, int statusCode) => Provider(json, statusCode);

    private static OpenSearchIndexingProviderAsync Provider(string json, int statusCode, bool throwExceptions = false,
        IOpenSearchClientSettingsManager? settingsManager = null) =>
        Provider(new InMemoryConnection(Encoding.UTF8.GetBytes(json), statusCode), throwExceptions, settingsManager);

    private static OpenSearchIndexingProviderAsync Provider(IConnection connection, bool throwExceptions = false,
        IOpenSearchClientSettingsManager? settingsManager = null)
    {
        var settings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://localhost:9200")), connection)
            .ThrowExceptions(throwExceptions);
        return new OpenSearchIndexingProviderAsync(new FixedClientFactory(new OpenSearchClient(settings)), NullLogger<OpenSearchIndexingProviderAsync>.Instance, settingsManager ?? NoMappings);
    }

    /// <summary>A provider that talks to <paramref name="port"/> on this machine over the real HTTP connection.</summary>
    private static OpenSearchIndexingProviderAsync LocalProvider(int port)
    {
        var settings = new ConnectionSettings(new Uri($"http://127.0.0.1:{port}"));
        return new OpenSearchIndexingProviderAsync(new FixedClientFactory(new OpenSearchClient(settings)), NullLogger<OpenSearchIndexingProviderAsync>.Instance, NoMappings);
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
    public async Task EnsureIndexExistsAsync_MappingsNotAcknowledged_ReturnsFailureSayingTheyMayStillApply()
    {
        // The in-memory connection answers every request with this body: the exists check with 200, then the put-mapping.
        var provider = Provider("""{"acknowledged":false}""", 200, settingsManager: WithMappings());

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldBe("Adding the configured mappings to index people was not acknowledged in time. The mappings are not rolled back and may still apply; call EnsureIndexExistsAsync again to confirm them.");
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_MappingsAcknowledged_ReturnsTrueForAnExistingIndex()
    {
        var provider = Provider("""{"acknowledged":true}""", 200, settingsManager: WithMappings());

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task IndexExistsAsync_ServerError_ReturnsFailure(int statusCode)
    {
        var provider = Provider("""{"error":{"type":"security_exception","reason":"no permissions"},"status":403}""", statusCode);

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldStartWith("OpenSearch index exists check failed");
        result.Errors.Single().Message.ShouldContain(statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        result.Errors.Single().Message.ShouldNotContain("Audit trail");
    }

    [Fact]
    public async Task IndexExistsAsync_ConnectionRefused_ReturnsFailureCausedByTheTransportException()
    {
        // With ThrowExceptions off, which is how the provider's clients are built, there is no status code and the
        // connection's exception is only on the response.
        var provider = LocalProvider(ClosedPort());

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldStartWith("OpenSearch index exists check failed");
        result.Errors.Single().Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeOfType<HttpRequestException>();
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(404, false)]
    public async Task IndexExistsAsync_Answered_ReturnsWhetherItExists(int statusCode, bool exists)
    {
        var provider = Provider("", statusCode);

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(exists);
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

    private static IOpenSearchClientSettingsManager WithBulkBatchSize(int? bulkBatchSize)
    {
        var monitor = InvalidSettingsOptions();
        monitor.CurrentValue.SharedIndexSettings.BulkBatchSize = bulkBatchSize;
        return new OpenSearchClientSettingsManager(monitor);
    }

    private static List<SearchDocument> PeopleDocuments(int count) =>
        Enumerable.Range(1, count).Select(i => new SearchDocument
        {
            Id = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Type = "TestDocument",
            Index = "people",
        }).ToList();

    /// <summary>A bulk index response with one created item for each id, except the rejected ones.</summary>
    private static InMemoryConnection BulkIndexed(IEnumerable<int> ids, params int[] rejected) =>
        new(Encoding.UTF8.GetBytes("{\"took\":1,\"errors\":" + (rejected.Length > 0 ? "true" : "false") + ",\"items\":[" + string.Join(",", ids.Select(id => rejected.Contains(id)
            ? $$$$"""{"index":{"_index":"people","_id":"{{{{id}}}}","status":400,"error":{"type":"mapper_parsing_exception","reason":"failed to parse field [Count]"}}}"""
            : $$$"""{"index":{"_index":"people","_id":"{{{id}}}","_version":1,"result":"created","status":201}}"""))
            + "]}"), 200);

    private static InMemoryConnection BulkForbidden() =>
        new(Encoding.UTF8.GetBytes("""{"error":{"type":"security_exception","reason":"no permissions"},"status":403}"""), 403);

    [Fact]
    public async Task IndexMultipleDocumentsAsync_MoreDocumentsThanBulkBatchSize_SendsOneRequestPerBatchAndCountsThemAll()
    {
        var connection = new SequenceConnection(BulkIndexed([1, 2]), BulkIndexed([3, 4]), BulkIndexed([5]));
        var provider = Provider(connection, settingsManager: WithBulkBatchSize(2));

        var result = await provider.IndexMultipleDocumentsAsync(PeopleDocuments(5), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
        connection.Requests.ShouldBe(3);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_BulkBatchSizeUnset_SendsAThousandDocumentsPerRequest()
    {
        var connection = new SequenceConnection(BulkIndexed(Enumerable.Range(1, 1000)), BulkIndexed([1001]));
        var provider = Provider(connection);

        var result = await provider.IndexMultipleDocumentsAsync(PeopleDocuments(1001), Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1001);
        connection.Requests.ShouldBe(2);
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_ItemRejectedInALaterBatch_SendsEveryBatchAndNamesTheRejectedIds()
    {
        var connection = new SequenceConnection(BulkIndexed([1, 2]), BulkIndexed([3, 4], rejected: 4), BulkIndexed([5]));
        var provider = Provider(connection, settingsManager: WithBulkBatchSize(2));

        var result = await provider.IndexMultipleDocumentsAsync(PeopleDocuments(5), Token);

        connection.Requests.ShouldBe(3);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldBe("Indexing documents into people failed for ids 4: failed to parse field [Count]. 4 of 5 documents were indexed.");
        error.Metadata["ids"].ShouldBe(new List<string> { "4" });
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_LaterBatchRejectedAsAWhole_StopsAndNamesEveryUnconfirmedId()
    {
        var connection = new SequenceConnection(BulkIndexed([1, 2], rejected: 1), BulkForbidden());
        var provider = Provider(connection, settingsManager: WithBulkBatchSize(2));

        var result = await provider.IndexMultipleDocumentsAsync(PeopleDocuments(5), Token);

        connection.Requests.ShouldBe(2);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Message.ShouldBe("Indexing documents into people failed for ids 1, 3, 4, 5: OpenSearch bulk index failed with HTTP 403 (security_exception): no permissions. "
            + "That was the bulk request for documents 3 to 4, which may or may not be stored, and the 1 documents after them were not sent. 1 of 5 documents were indexed.");
        error.Metadata["ids"].ShouldBe(new List<string> { "1", "3", "4", "5" });
    }

    [Fact]
    public async Task IndexMultipleDocumentsAsync_FirstBatchRejectedAsAWhole_ReturnsTheRequestError()
    {
        var connection = new SequenceConnection(BulkForbidden());
        var provider = Provider(connection, settingsManager: WithBulkBatchSize(2));

        var result = await provider.IndexMultipleDocumentsAsync(PeopleDocuments(5), Token);

        connection.Requests.ShouldBe(1);
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("OpenSearch bulk index failed with HTTP 403 (security_exception): no permissions");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task IndexMultipleDocumentsAsync_BulkBatchSizeNotPositive_ReturnsFailureNamingTheSetting(int bulkBatchSize)
    {
        var connection = new SequenceConnection(BulkIndexed([1]));
        var provider = Provider(connection, settingsManager: WithBulkBatchSize(bulkBatchSize));

        var result = await provider.IndexMultipleDocumentsAsync(PeopleDocuments(1), Token);

        connection.Requests.ShouldBe(0);
        result.Errors.ShouldHaveSingleItem().Message.ShouldBe(
            $"BulkBatchSize is {bulkBatchSize} for index people, but it must be greater than 0. Set Bielu:BetterSearch:OpenSearch:SharedIndexSettings:BulkBatchSize, or BulkBatchSize on the index's Indexes entry.");
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
        message.ShouldBe("OpenSearch bulk delete failed with HTTP 403 (security_exception): no permissions");
    }

    private static InMemoryConnection Answer(string json, int statusCode) => new(Encoding.UTF8.GetBytes(json), statusCode);

    private const string DeletedTwo = """{"took":5,"timed_out":false,"total":2,"deleted":2,"batches":1,"version_conflicts":0,"noops":0,"failures":[]}""";

    [Fact]
    public async Task RemoveAllDocumentsAsync_RefreshForbidden_ReturnsTheRefreshFailureWithoutDeleting()
    {
        var connection = new SequenceConnection(
            Answer("""{"error":{"type":"security_exception","reason":"no permissions for [indices:admin/refresh]"},"status":403}""", 403),
            Answer(DeletedTwo, 200));
        var provider = Provider(connection);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("OpenSearch refresh of index people before removing all documents failed with HTTP 403 (security_exception): no permissions for [indices:admin/refresh]");
        connection.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_RefreshFailsOnAShard_ReturnsFailureWithoutDeleting()
    {
        var connection = new SequenceConnection(
            Answer("""{"_shards":{"total":2,"successful":1,"failed":1}}""", 200),
            Answer(DeletedTwo, 200));
        var provider = Provider(connection);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.Errors.ShouldHaveSingleItem().Message.ShouldBe("Refreshing index people before removing all documents failed on 1 of 2 shards, so no documents were removed.");
        connection.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_Refreshed_ReturnsTheDeletedCount()
    {
        var connection = new SequenceConnection(
            Answer("""{"_shards":{"total":2,"successful":1,"failed":0}}""", 200),
            Answer(DeletedTwo, 200));
        var provider = Provider(connection);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        connection.Requests.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_TimedOut_ReturnsFailureWithDeletedCount()
    {
        var provider = Provider("""{"took":30000,"timed_out":true,"total":10,"deleted":4,"batches":1,"version_conflicts":0,"noops":0,"failures":[]}""", 200);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("4 of 10");
    }

    [Fact]
    public async Task RemoveAllDocumentsAsync_WithFailures_ReturnsFailure()
    {
        var provider = Provider("""{"took":5,"timed_out":false,"total":10,"deleted":9,"batches":1,"version_conflicts":1,"noops":0,"failures":[{"index":"people","id":"3","status":409,"cause":{"type":"version_conflict_engine_exception","reason":"conflict"}}]}""", 200);

        var result = await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("failures: 1");
    }

    [Fact]
    public async Task CreateIndexAsync_Forbidden_ReturnsFailureWithTheCause()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"security_exception","reason":"no permissions for [indices:admin/create]"}],"type":"security_exception","reason":"no permissions for [indices:admin/create]"},"status":403}""", 403);

        var result = await provider.CreateIndexAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("security_exception");
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_CreateForbidden_ReturnsFailure()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"security_exception","reason":"no permissions"}],"type":"security_exception","reason":"no permissions"},"status":403}""", 403);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_CreatedByAnotherCallerFirst_ReturnsTrue()
    {
        // The exists check sees no index, then another caller creates it before this caller's create.
        var alreadyExists = """{"error":{"root_cause":[{"type":"resource_already_exists_exception","reason":"index [people/abc] already exists","index":"people"}],"type":"resource_already_exists_exception","reason":"index [people/abc] already exists","index":"people"},"status":400}""";
        var provider = Provider(new HeadRoutingConnectionDecorator(
            new InMemoryConnection(Encoding.UTF8.GetBytes("{}"), 404),
            new InMemoryConnection(Encoding.UTF8.GetBytes(alreadyExists), 400)));

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_CreatedByAnotherCallerFirstWithMappings_AddsThemAndReturnsTrue()
    {
        // The exists check sees no index, another caller creates it before this caller's create, so the mappings are put.
        var alreadyExists = """{"error":{"root_cause":[{"type":"resource_already_exists_exception","reason":"index [people/abc] already exists","index":"people"}],"type":"resource_already_exists_exception","reason":"index [people/abc] already exists","index":"people"},"status":400}""";
        var connection = new SequenceConnection(
            new InMemoryConnection(Encoding.UTF8.GetBytes("{}"), 404),
            new InMemoryConnection(Encoding.UTF8.GetBytes(alreadyExists), 400),
            new InMemoryConnection(Encoding.UTF8.GetBytes("""{"acknowledged":true}"""), 200));
        var provider = Provider(connection, settingsManager: WithMappings());

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
        connection.Requests.ShouldBe(3);
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_ExistsCheckForbidden_ReturnsTheCheckFailure()
    {
        // Every request gets the same 401; a create attempt would fail with a create error instead.
        var provider = Provider("""{"error":{"type":"security_exception","reason":"missing authentication credentials"},"status":401}""", 401);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldStartWith("OpenSearch index exists check failed with HTTP 401");
    }

    [Fact]
    public async Task IndexingProvider_InvalidSettings_ReturnsFailuresInsteadOfThrowing()
    {
        var provider = new OpenSearchIndexingProviderAsync(InvalidSettingsFactory(), NullLogger<OpenSearchIndexingProviderAsync>.Instance, NoMappings);
        var document = new SearchDocument { Id = "1", Index = "people" };

        (await provider.IndexDocumentAsync(document, Token)).IsFailed.ShouldBeTrue();
        (await provider.IndexMultipleDocumentsAsync([document], Token)).IsFailed.ShouldBeTrue();
        (await provider.RemoveDocumentAsync("1", "people", Token)).IsFailed.ShouldBeTrue();
        (await provider.RemoveDocumentsAsync("people", ["1"], Token)).IsFailed.ShouldBeTrue();
        (await provider.RemoveAllDocumentsAsync(new DeleteAllDocumentsRequest { IndexName = "people" }, Token)).IsFailed.ShouldBeTrue();
        (await provider.DeleteIndexAsync("people", Token)).IsFailed.ShouldBeTrue();
        var exists = await provider.IndexExistsAsync("people", Token);
        exists.IsFailed.ShouldBeTrue();
        exists.Errors.Single().Message.ShouldStartWith("The OpenSearch settings for people are invalid:");
        var create = await provider.CreateIndexAsync("people", Token);
        create.IsFailed.ShouldBeTrue();
        create.Errors.Single().Message.ShouldStartWith("The OpenSearch settings for people are invalid:");
        var ensure = await provider.EnsureIndexExistsAsync("people", Token);
        ensure.IsFailed.ShouldBeTrue();
        ensure.Errors.Single().Message.ShouldStartWith("The OpenSearch settings for people are invalid:");
        ensure.Errors.Single().Message.ShouldContain("not an http or https URL");
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

    private static async Task<IResultBase> InvokeAsync(OpenSearchIndexingProviderAsync provider, string method, CancellationToken cancellationToken)
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
    public async Task IndexingMethod_RequestThrows_ReturnsFailureCausedByTheException(string method)
    {
        // With ThrowExceptions on, a failed request throws an OpenSearchClientException rather than returning a response.
        var provider = Provider("""{"error":"unavailable","status":503}""", 503, throwExceptions: true);

        var result = await InvokeAsync(provider, method, Token);

        result.IsFailed.ShouldBeTrue();
        var exception = result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeOfType<OpenSearchClientException>();
        result.Errors.Select(e => e.Message).ShouldBe([exception.Message]);
    }

    [Theory]
    [MemberData(nameof(IndexingMethods))]
    public async Task IndexingMethod_ClientFactoryThrows_ReturnsFailureCausedByTheException(string method)
    {
        // Not an ArgumentException, so TryGetClientAsync does not turn it into an invalid settings failure.
        var exception = new InvalidOperationException("bad connection string");
        var provider = new OpenSearchIndexingProviderAsync(new ThrowingClientFactory(exception), NullLogger<OpenSearchIndexingProviderAsync>.Instance, NoMappings);

        var result = await InvokeAsync(provider, method, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["bad connection string"]);
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
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
        var provider = new OpenSearchIndexingProviderAsync(new CancelledClientFactory(), NullLogger<OpenSearchIndexingProviderAsync>.Instance, NoMappings);

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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_MaxResultWindowNotPositive_ReturnsConfigurationError(int maxResultWindow)
    {
        var translateService = new OpenSearchQueryTranslateService([]);
        var provider = new OpenSearchSearchProviderAsync(translateService, translateService, InvalidSettingsFactory(), new OpenSearchResultMapper(), InvalidSettingsOptions(maxResultWindow), NoMappings);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe($"MaxResultWindow is {maxResultWindow}, but it must be greater than 0. Set Bielu:BetterSearch:OpenSearch:MaxResultWindow to the indexes' index.max_result_window.");
    }

    [Fact]
    public async Task SearchAsync_InvalidSettings_ReturnsFailureInsteadOfThrowing()
    {
        var options = InvalidSettingsOptions();
        var translateService = new OpenSearchQueryTranslateService([]);
        var provider = new OpenSearchSearchProviderAsync(translateService, translateService, InvalidSettingsFactory(), new OpenSearchResultMapper(), options, NoMappings);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" }, TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("people");
    }

    /// <summary>Gives prefix <c>blue-</c> until the first read after <see cref="Arm"/>, and <c>green-</c> from then on, as a configuration reload would.</summary>
    private sealed class ReloadingPrefixOptionsMonitor : IOptionsMonitor<OpenSearchOptions>
    {
        private readonly OpenSearchOptions blue = new() { IndexPrefix = "blue-", SharedIndexSettings = new() { ConnectionString = "localhost:9200" } };
        private readonly OpenSearchOptions green = new() { IndexPrefix = "green-", SharedIndexSettings = new() { ConnectionString = "localhost:9200" } };
        private bool armed;
        private bool reloaded;

        public void Arm() => armed = true;

        public OpenSearchOptions CurrentValue
        {
            get
            {
                var value = reloaded ? green : blue;
                reloaded |= armed;
                return value;
            }
        }

        public OpenSearchOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }

    private static readonly DateTimeOffset RebuildStarted = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompleteRebuildAsync_RefreshFailedOnSomeShards_FailsWithoutTouchingTheAlias()
    {
        var connection = new SequenceConnection(
            Answer("""{"_shards":{"total":2,"successful":1,"failed":1,"failures":[{"shard":0,"index":"people","status":"INTERNAL_SERVER_ERROR","reason":{"type":"exception","reason":"refresh failed"}}]}}""", 200));
        var provider = Provider(connection);
        var rebuild = new Abstractions.Models.IndexRebuild("people", IndexNames.NewRebuildTarget("people", RebuildStarted));

        var result = await provider.CompleteRebuildAsync(rebuild, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("failed on 1 of 2 shards");
        connection.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task CompleteRebuildAsync_PrefixReloadedDuringTheCall_UsesTheAliasItChecked()
    {
        var monitor = new ReloadingPrefixOptionsMonitor();
        var settingsManager = new OpenSearchClientSettingsManager(monitor);
        // Refresh, alias lookup (no alias), exists check (no index), a rejected alias update, then the alias read again.
        var connection = new SequenceConnection(
            Answer("""{"_shards":{"total":1,"successful":1,"failed":0}}""", 200),
            Answer("""{"error":"alias [blue-people] missing","status":404}""", 404),
            Answer("", 404),
            Answer("""{"error":{"root_cause":[{"type":"illegal_argument_exception","reason":"rejected"}],"type":"illegal_argument_exception","reason":"rejected"},"status":400}""", 400),
            Answer("""{"error":"alias [blue-people] missing","status":404}""", 404));
        var provider = Provider(connection, settingsManager: settingsManager);
        var rebuild = new Abstractions.Models.IndexRebuild("people", IndexNames.NewRebuildTarget("blue-people", RebuildStarted));
        monitor.Arm();

        var result = await provider.CompleteRebuildAsync(rebuild, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("pointing alias blue-people at");
        connection.Requests.ShouldBe(5);
    }
}
