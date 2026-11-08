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
        var manager = new FailingLookupIndexManager("lookup failed");
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
    public async Task GetIndexAsync_DefaultImplementation_MissingIndex_ReturnsNull()
    {
        ILiftiIndexManager manager = new DefaultLookupLiftiIndexManagerWrapper(new LiftiIndexManager());

        var result = await manager.GetIndexAsync("test-index");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        (await manager.ExistsAsync("test-index")).Value.ShouldBeFalse();
    }

    [Fact]
    public async Task GetIndexAsync_DefaultImplementation_ExistingIndex_ReturnsIndex()
    {
        var inner = new LiftiIndexManager();
        var created = (await inner.GetOrCreateIndexAsync("test-index")).Value;
        ILiftiIndexManager manager = new DefaultLookupLiftiIndexManagerWrapper(inner);

        var result = await manager.GetIndexAsync("test-index");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(created);
    }

    [Fact]
    public async Task GetIndexAsync_DefaultImplementation_ExistsFails_ReturnsOriginalErrors()
    {
        ILiftiIndexManager manager = new DefaultLookupLiftiIndexManagerWrapper(new LiftiIndexManager(), existsError: "exists failed");

        var result = await manager.GetIndexAsync("test-index");

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["exists failed"]);
    }

    /// <summary>Implements every member except <see cref="ILiftiIndexManager.GetIndexAsync"/>, so the default is used.</summary>
    private sealed class DefaultLookupLiftiIndexManagerWrapper(LiftiIndexManager inner, string? existsError = null) : ILiftiIndexManager
    {
        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) => inner.GetOrCreateIndexAsync(key);

        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) => inner.CreateIndexAsync(key);

        public Task<Result<bool>> ExistsAsync(string name) =>
            existsError is null ? inner.ExistsAsync(name) : Task.FromResult(Result.Fail<bool>(existsError));

        public Task<Result<bool>> DeleteIndexAsync(string name) => inner.DeleteIndexAsync(name);
    }

    private sealed class FailingLookupIndexManager(string error) : ILiftiIndexManager
    {
        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) =>
            throw new InvalidOperationException("The index must not be created.");

        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) =>
            throw new InvalidOperationException("The index must not be created.");

        public Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key) =>
            Task.FromResult(Result.Fail<IFullTextIndex<string>?>(error));

        public Task<Result<bool>> ExistsAsync(string name) => Task.FromResult(Result.Ok(false));

        public Task<Result<bool>> DeleteIndexAsync(string name) => Task.FromResult(Result.Ok(false));
    }
}
