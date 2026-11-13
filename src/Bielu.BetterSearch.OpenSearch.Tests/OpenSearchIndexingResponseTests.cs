using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
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
public sealed class OpenSearchIndexingResponseTests
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
        new OpenSearchClientSettingsManager(new InvalidSettingsOptionsMonitor());

    private sealed class InvalidSettingsOptionsMonitor(int maxResultWindow = 10_000) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue { get; } = new() { SharedIndexSettings = new() { ConnectionString = "localhost:9200" }, MaxResultWindow = maxResultWindow };
        public OpenSearchOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }

    private static IClientFactoryAsync<IOpenSearchClient> InvalidSettingsFactory() =>
        new OpenSearchClientFactory(new OpenSearchClientSettingsManager(new InvalidSettingsOptionsMonitor()));

    private static IOpenSearchClientSettingsManager WithMappings()
    {
        var monitor = new InvalidSettingsOptionsMonitor();
        monitor.CurrentValue.SharedIndexSettings.Mappings = [new() { Field = "City", Type = OpenSearchFieldType.Keyword }];
        return new OpenSearchClientSettingsManager(monitor);
    }

    private static OpenSearchIndexingProviderAsync Provider(string json, int statusCode, bool throwExceptions = false,
        IOpenSearchClientSettingsManager? settingsManager = null)
    {
        var connection = new InMemoryConnection(Encoding.UTF8.GetBytes(json), statusCode);
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

    [Fact]
    public async Task IndexExistsAsync_CancelledMidRequest_ThrowsOperationCanceledException()
    {
        // The server accepts the connection and never answers, so the request is still in flight when it is cancelled.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var provider = LocalProvider(((IPEndPoint)listener.LocalEndpoint).Port);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

            await Should.ThrowAsync<OperationCanceledException>(() => provider.IndexExistsAsync("people", cancellation.Token));
        }
        finally
        {
            listener.Stop();
        }
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

    [Fact]
    public async Task RemoveDocumentsAsync_CancelledMidRequest_ThrowsOperationCanceledException()
    {
        // The server accepts the connection and never answers, so the request is still in flight when it is cancelled.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var provider = LocalProvider(((IPEndPoint)listener.LocalEndpoint).Port);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

            await Should.ThrowAsync<OperationCanceledException>(() => provider.RemoveDocumentsAsync("people", ["1", "2"], cancellation.Token));
        }
        finally
        {
            listener.Stop();
        }
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
    public async Task IndexingMethod_ClientFactoryCancelled_ThrowsOperationCanceledException(string method)
    {
        var provider = new OpenSearchIndexingProviderAsync(new CancelledClientFactory(), NullLogger<OpenSearchIndexingProviderAsync>.Instance, NoMappings);

        await Should.ThrowAsync<OperationCanceledException>(() => InvokeAsync(provider, method, Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_MaxResultWindowNotPositive_ReturnsConfigurationError(int maxResultWindow)
    {
        var translateService = new OpenSearchQueryTranslateService([]);
        var provider = new OpenSearchSearchProviderAsync(translateService, translateService, InvalidSettingsFactory(), new OpenSearchResultMapper(), new InvalidSettingsOptionsMonitor(maxResultWindow), NoMappings);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe($"MaxResultWindow is {maxResultWindow}, but it must be greater than 0. Set Bielu:BetterSearch:OpenSearch:MaxResultWindow to the indexes' index.max_result_window.");
    }

    [Fact]
    public async Task SearchAsync_InvalidSettings_ReturnsFailureInsteadOfThrowing()
    {
        var options = new InvalidSettingsOptionsMonitor();
        var translateService = new OpenSearchQueryTranslateService([]);
        var provider = new OpenSearchSearchProviderAsync(translateService, translateService, InvalidSettingsFactory(), new OpenSearchResultMapper(), options, NoMappings);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("people");
    }
}
