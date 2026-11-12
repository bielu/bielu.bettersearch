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
    public async Task RemoveDocumentAsync_IndexDeletedAfterLookup_DoesNotRecreateIndex()
    {
        var manager = new GatedLookupLiftiIndexManager();
        await manager.GetOrCreateIndexAsync("test-index");
        // Two providers sharing one manager, as two DI scopes do.
        var remover = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);
        var deleter = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);

        var remove = remover.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken);
        await manager.LookedUp.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var delete = deleter.DeleteIndexAsync("test-index", TestContext.Current.CancellationToken);
        await Task.WhenAny(delete, Task.Delay(200, TestContext.Current.CancellationToken));
        var deleteFinishedDuringRemove = delete.IsCompleted;
        manager.Release.SetResult();

        (await remove).IsSuccess.ShouldBeTrue();
        (await delete).Value.ShouldBeTrue();
        deleteFinishedDuringRemove.ShouldBeFalse();
        (await manager.ExistsAsync("test-index")).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveDocumentsAsync_IndexLookupFails_ReturnsOriginalErrors()
    {
        var manager = new FakeLiftiIndexManager("lookup failed", creationThrows: true);
        var provider = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);

        var result = await provider.RemoveDocumentsAsync("test-index", ["1", "2"], TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["lookup failed"]);
    }

    [Fact]
    public async Task RemoveDocumentsAsync_MissingIndex_ReturnsZeroWithoutCreatingIndex()
    {
        var manager = new LiftiIndexManager();
        var provider = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);

        var result = await provider.RemoveDocumentsAsync("test-index", ["1", "2"], TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
        (await manager.ExistsAsync("test-index")).Value.ShouldBeFalse();
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

    /// <summary>Pauses <see cref="GetIndexAsync"/> after the lookup until <see cref="Release"/> is set.</summary>
    private sealed class GatedLookupLiftiIndexManager : ILiftiIndexManager
    {
        private readonly LiftiIndexManager _inner = new();

        public TaskCompletionSource LookedUp { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key)
        {
            var index = await _inner.GetIndexAsync(key);
            LookedUp.TrySetResult();
            await Release.Task;
            return index;
        }

        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) => _inner.GetOrCreateIndexAsync(key);

        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) => _inner.CreateIndexAsync(key);

        public Task<Result<bool>> ExistsAsync(string name) => _inner.ExistsAsync(name);

        public Task<Result<bool>> DeleteIndexAsync(string name) => _inner.DeleteIndexAsync(name);
    }
}
