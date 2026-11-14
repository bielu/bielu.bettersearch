using System.Text;
using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.OpenSearch.Configuration;
using Bielu.BetterSearch.OpenSearch.Services;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;
using Shouldly;

namespace Bielu.BetterSearch.OpenSearch.Tests;

/// <summary>
/// 404 answers to <see cref="ISearchProviderAsync.GetDocumentAsync"/>, served from an in-memory connection.
/// </summary>
public sealed class OpenSearchGetDocumentResponseTests
{
    private sealed class FixedClientFactory(IOpenSearchClient client) : IClientFactoryAsync<IOpenSearchClient>
    {
        public Task<IOpenSearchClient> GetOrCreateClientAsync(string indexName) => Task.FromResult(client);
    }

    private sealed class FixedOptionsMonitor(OpenSearchOptions value) : IOptionsMonitor<OpenSearchOptions>
    {
        public OpenSearchOptions CurrentValue => value;
        public OpenSearchOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<OpenSearchOptions, string?> listener) => null;
    }

    private static OpenSearchSearchProviderAsync Provider(string body, string contentType = "application/json")
    {
        var connection = new InMemoryConnection(Encoding.UTF8.GetBytes(body), 404, null, contentType);
        var client = new OpenSearchClient(new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://localhost:9200")), connection));
        var options = new FixedOptionsMonitor(new OpenSearchOptions { SharedIndexSettings = new() });
        var translateService = new OpenSearchQueryTranslateService([]);
        return new OpenSearchSearchProviderAsync(translateService, translateService, new FixedClientFactory(client),
            new OpenSearchResultMapper(), options, new OpenSearchClientSettingsManager(options));
    }

    [Fact]
    public async Task GetDocumentAsync_DocumentNotFound_ReturnsNull()
    {
        var provider = Provider("""{"_index":"people","_id":"1","found":false}""");

        var result = await provider.GetDocumentAsync("people", "1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task GetDocumentAsync_IndexNotFound_ReturnsNull()
    {
        var provider = Provider("""{"error":{"root_cause":[{"type":"index_not_found_exception","reason":"no such index [people]","index":"people"}],"type":"index_not_found_exception","reason":"no such index [people]","index":"people"},"status":404}""");

        var result = await provider.GetDocumentAsync("people", "1", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("{}", "application/json")]
    [InlineData("<html><body>Not Found</body></html>", "text/html")]
    public async Task GetDocumentAsync_NotFoundWithoutAnEngineAnswer_ReturnsFailure(string body, string contentType)
    {
        var provider = Provider(body, contentType);

        var result = await provider.GetDocumentAsync("people", "1", TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldContain("404");
    }
}
