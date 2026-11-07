using System.Text;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
}
