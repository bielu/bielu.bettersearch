using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public interface ILiftiIndexManager
{
    public IFullTextIndex<string> GetOrCreateIndexAsync(string key);
}
