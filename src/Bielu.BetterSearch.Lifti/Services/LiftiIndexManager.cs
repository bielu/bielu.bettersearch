using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexManager : ILiftiIndexManager
{
    Dictionary<string,IFullTextIndex<string>> _indices = new();
    public IFullTextIndex<string> GetOrCreateIndexAsync(string key)
    {
        if(_indices.TryGetValue(key, out var index))
        {
            return index;
        }
        index = new FullTextIndexBuilder<string>().Build();;
        _indices.Add(key, index);
        return index;
    }
}
