using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public interface ILiftiIndexManager
{
    public IFullTextIndex<string> GetOrCreateIndexAsync(string key);
    public IFullTextIndex<string>? CreateIndexAsync(string key);
    Task<Result<bool>> ExistsAsync(string name);
    Task<Result<bool>> DeleteIndexAsync(string name);
}
