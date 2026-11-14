using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Meilisearch.Configuration;
using Bielu.BetterSearch.Meilisearch.Services;
using Bielu.BetterSearch.Tests;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using MeilisearchClient = Meilisearch.MeilisearchClient;

namespace Bielu.BetterSearch.Meilisearch.Tests;

/// <summary>A client factory that throws before any request is made, so no engine is needed.</summary>
public sealed class MeilisearchClientFactoryExceptionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MeilisearchIndexingProviderAsync Provider(Exception exception) =>
        new(new ThrowingClientFactory(exception), new TestOptionsMonitor<MeilisearchOptions>(new MeilisearchOptions()), NullLogger<MeilisearchIndexingProviderAsync>.Instance);

    [Fact]
    public async Task IndexDocumentAsync_ClientFactoryThrows_ReturnsFailureCausedByTheException()
    {
        var exception = new InvalidOperationException("bad connection string");

        var result = await Provider(exception).IndexDocumentAsync(new SearchDocument { Id = "1", Index = "people" }, Token);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["bad connection string"]);
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task IndexDocumentAndIndexMultipleDocuments_ClientFactoryCancelled_ThrowsOperationCanceledException()
    {
        // The settings check gets the client before any write does, so this is where cancellation must not become a failure.
        var provider = Provider(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() => provider.IndexDocumentAsync(new SearchDocument { Id = "1", Index = "people" }, Token));
        await Should.ThrowAsync<OperationCanceledException>(() => provider.IndexMultipleDocumentsAsync([new SearchDocument { Id = "1", Index = "people" }], Token));
    }

    private sealed class ThrowingClientFactory(Exception exception) : IClientFactoryAsync<MeilisearchClient>
    {
        public Task<MeilisearchClient> GetOrCreateClientAsync(string indexName) => throw exception;
    }
}
