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
