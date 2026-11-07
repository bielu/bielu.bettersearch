using System.Text;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
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

    private sealed class InvalidSettingsOptionsMonitor(int maxResultWindow = 10_000) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue { get; } = new() { SharedIndexSettings = new() { ConnectionString = "localhost:9200" }, MaxResultWindow = maxResultWindow };
        public OpenSearchOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }

    private static IClientFactoryAsync<IOpenSearchClient> InvalidSettingsFactory() =>
        new OpenSearchClientFactory(new OpenSearchClientSettingsManager(new InvalidSettingsOptionsMonitor()));

    private static OpenSearchIndexingProviderAsync Provider(string json, int statusCode)
    {
        var connection = new InMemoryConnection(Encoding.UTF8.GetBytes(json), statusCode);
        var settings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://localhost:9200")), connection);
        return new OpenSearchIndexingProviderAsync(new FixedClientFactory(new OpenSearchClient(settings)), NullLogger<OpenSearchIndexingProviderAsync>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task IndexExistsAsync_ServerError_ReturnsFailure(int statusCode)
    {
        var provider = Provider("""{"error":{"type":"security_exception","reason":"no permissions"},"status":403}""", statusCode);

        var result = await provider.IndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
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
    public async Task EnsureIndexExistsAsync_ExistsCheckForbidden_ReturnsTheCheckFailure()
    {
        // Every request gets the same 401; a create attempt would fail with a create error instead.
        var provider = Provider("""{"error":{"type":"security_exception","reason":"missing authentication credentials"},"status":401}""", 401);

        var result = await provider.EnsureIndexExistsAsync("people", Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("HEAD");
    }

    [Fact]
    public async Task IndexingProvider_InvalidSettings_ReturnsFailuresInsteadOfThrowing()
    {
        var provider = new OpenSearchIndexingProviderAsync(InvalidSettingsFactory(), NullLogger<OpenSearchIndexingProviderAsync>.Instance);
        var document = new SearchDocument { Id = "1", Index = "people" };

        (await provider.IndexDocumentAsync(document, Token)).IsFailed.ShouldBeTrue();
        (await provider.IndexMultipleDocumentsAsync([document], Token)).IsFailed.ShouldBeTrue();
        (await provider.RemoveDocumentAsync("1", "people", Token)).IsFailed.ShouldBeTrue();
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

    [Fact]
    public async Task IndexExistsAndCreateIndex_Cancelled_Throw()
    {
        // Cancellation is not a failed Result: it propagates, as it does from every other method.
        var provider = new OpenSearchIndexingProviderAsync(new CancelledClientFactory(), NullLogger<OpenSearchIndexingProviderAsync>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => provider.IndexExistsAsync("people", Token));
        await Should.ThrowAsync<OperationCanceledException>(() => provider.CreateIndexAsync("people", Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_MaxResultWindowNotPositive_ReturnsConfigurationError(int maxResultWindow)
    {
        var translateService = new OpenSearchQueryTranslateService([]);
        var provider = new OpenSearchSearchProviderAsync(translateService, translateService, InvalidSettingsFactory(), new OpenSearchResultMapper(), new InvalidSettingsOptionsMonitor(maxResultWindow));

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe($"MaxResultWindow is {maxResultWindow}, but it must be greater than 0. Set Bielu:BetterSearch:OpenSearch:MaxResultWindow to the indexes' index.max_result_window.");
    }

    [Fact]
    public async Task SearchAsync_InvalidSettings_ReturnsFailureInsteadOfThrowing()
    {
        var options = new InvalidSettingsOptionsMonitor();
        var translateService = new OpenSearchQueryTranslateService([]);
        var provider = new OpenSearchSearchProviderAsync(translateService, translateService, InvalidSettingsFactory(), new OpenSearchResultMapper(), options);

        var result = await provider.SearchAsync(new SearchQuery { Index = "people" });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("people");
    }
}
