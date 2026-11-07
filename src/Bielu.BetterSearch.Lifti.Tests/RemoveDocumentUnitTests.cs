using Bielu.BetterSearch.Lifti.Services;
using FluentResults;
using Lifti;
using Shouldly;

namespace Bielu.BetterSearch.Lifti.Tests;

public class RemoveDocumentUnitTests
{
    [Fact]
    public async Task RemoveDocumentAsync_IndexExistsCheckFails_ReturnsOriginalErrors()
    {
        var manager = new FailingExistsIndexManager("exists check failed");
        var provider = new LiftiIndexingProviderAsync(new LiftiClientFactoryAsync(manager), manager);

        var result = await provider.RemoveDocumentAsync("1", "test-index", TestContext.Current.CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(e => e.Message).ShouldBe(["exists check failed"]);
    }

    private sealed class FailingExistsIndexManager(string error) : ILiftiIndexManager
    {
        public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) =>
            throw new InvalidOperationException("The index must not be created.");

        public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) =>
            throw new InvalidOperationException("The index must not be created.");

        public Task<Result<bool>> ExistsAsync(string name) => Task.FromResult(Result.Fail<bool>(error));

        public Task<Result<bool>> DeleteIndexAsync(string name) => Task.FromResult(Result.Ok(false));
    }
}
