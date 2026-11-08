using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public interface ILiftiIndexManager
{
    public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key);
    public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key);
    /// <summary>Returns the index, or <c>null</c> when it does not exist, without creating it.</summary>
    /// <remarks>The default implementation is not atomic; override it when the index store supports a single lookup.</remarks>
    async Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key)
    {
        var exists = await ExistsAsync(key).ConfigureAwait(false);
        if (exists.IsFailed)
        {
            return Result.Fail<IFullTextIndex<string>?>(exists.Errors);
        }

        if (!exists.Value)
        {
            return Result.Ok<IFullTextIndex<string>?>(null);
        }

        var index = await GetOrCreateIndexAsync(key).ConfigureAwait(false);
        return index.IsFailed
            ? Result.Fail<IFullTextIndex<string>?>(index.Errors)
            : Result.Ok<IFullTextIndex<string>?>(index.Value);
    }

    Task<Result<bool>> ExistsAsync(string name);
    Task<Result<bool>> DeleteIndexAsync(string name);
}
