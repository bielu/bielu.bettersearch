using Bielu.BetterSearch.Abstractions.Services;
using Bielu.BetterSearch.Lifti.Services;
using FluentResults;
using Lifti;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class RemoveDocumentUnitTests
{
    [Fact]
    public async Task RemoveDocumentAsync_IndexLookupFails_ReturnsOriginalErrors()
    {
        var manager = new FakeLiftiIndexManager("lookup failed", creationThrows: true);
        var provider = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);

        var result = await provider.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["lookup failed"]);
    }

    [Fact]
    public async Task RemoveDocumentAsync_MissingIndex_ReturnsSuccessWithoutCreatingIndex()
    {
        var manager = new LiftiIndexManager();
        var provider = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);

        var result = await provider.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        (await manager.ExistsAsync("test-index")).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveDocumentAsync_CustomClientFactory_RemovesFromTheFactoryIndex()
    {
        var manager = new LiftiIndexManager();
        var managerIndex = (await manager.GetOrCreateIndexAsync("test-index")).Value;
        var factoryIndex = (await new LiftiIndexManager().GetOrCreateIndexAsync("test-index")).Value;
        await managerIndex.AddAsync(Document("1"), TestContext.Current.CancellationToken);
        await factoryIndex.AddAsync(Document("1"), TestContext.Current.CancellationToken);
        var provider = new LiftiIndexingProviderAsync(new StubClientFactory(() => factoryIndex), manager);

        var result = await provider.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        factoryIndex.Count.ShouldBe(0);
        managerIndex.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveDocumentAsync_ClientFactoryThrows_ReturnsFailureCausedByTheException()
    {
        var manager = new LiftiIndexManager();
        await manager.GetOrCreateIndexAsync("test-index");
        var exception = new InvalidOperationException("factory failed");
        var provider = new LiftiIndexingProviderAsync(new StubClientFactory(() => throw exception), manager);

        var result = await provider.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("factory failed");
        result.Errors[0].Reasons.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task RemoveDocumentAsync_Cancelled_Throws()
    {
        var manager = new LiftiIndexManager();
        await manager.GetOrCreateIndexAsync("test-index");
        var provider = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.RemoveDocumentAsync("1", "test-index", cancellation.Token));
    }

    [Fact]
    public async Task RemoveDocumentAsync_FactoryThrowsCancellation_Rethrows()
    {
        var manager = new LiftiIndexManager();
        await manager.GetOrCreateIndexAsync("test-index");
        var provider = new LiftiIndexingProviderAsync(new StubClientFactory(() => throw new OperationCanceledException()), manager);

        await Should.ThrowAsync<OperationCanceledException>(() => provider.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIndexAsync_MissingIndex_ReturnsNullWithoutCreatingIndex()
    {
        var manager = new LiftiIndexManager();

        var result = await manager.GetIndexAsync("test-index");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        (await manager.ExistsAsync("test-index")).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task GetIndexAsync_ExistingIndex_ReturnsIndex()
    {
        var manager = new LiftiIndexManager();
        var created = (await manager.GetOrCreateIndexAsync("test-index")).Value;

        var result = await manager.GetIndexAsync("test-index");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(created);
    }

    private static SearchDocument Document(string id) => new()
    {
        Id = id,
        Index = "test-index",
        Fields = new Dictionary<string, IList<object>> { { "Name", ["alpha"] } }
    };

    private sealed class StubClientFactory(Func<IFullTextIndex<string>> getIndex) : IClientFactoryAsync<IFullTextIndex<string>>
    {
        public Task<IFullTextIndex<string>> GetOrCreateClientAsync(string indexName) => Task.FromResult(getIndex());
    }
}
