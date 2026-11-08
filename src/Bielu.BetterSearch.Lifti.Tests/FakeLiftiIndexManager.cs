using Bielu.BetterSearch.Lifti.Services;
using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Tests;

/// <summary>
/// An index manager that holds no index and fails with <paramref name="error"/>: looking an index up always fails, and
/// creating one fails too, unless <paramref name="creationThrows"/>, where creating one throws, for code that must not create it.
/// </summary>
internal sealed class FakeLiftiIndexManager(string error, bool creationThrows = false) : ILiftiIndexManager
{
    public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key) =>
        creationThrows ? throw new InvalidOperationException("The index must not be created.") : Task.FromResult(Result.Fail<IFullTextIndex<string>>(error));

    public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key) =>
        creationThrows ? throw new InvalidOperationException("The index must not be created.") : Task.FromResult(Result.Fail<IFullTextIndex<string>?>(error));

    public Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key) =>
        Task.FromResult(Result.Fail<IFullTextIndex<string>?>(error));

    public Task<Result<bool>> ExistsAsync(string name) => Task.FromResult(Result.Ok(false));

    public Task<Result<bool>> DeleteIndexAsync(string name) => Task.FromResult(Result.Ok(false));
}
