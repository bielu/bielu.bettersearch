using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public interface ILiftiIndexManager
{
    public Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key);
    public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key);
    /// <summary>Returns the index, or <c>null</c> when it does not exist, without creating it.</summary>
    Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key);
    Task<Result<bool>> ExistsAsync(string name);
    Task<Result<bool>> DeleteIndexAsync(string name);
}
