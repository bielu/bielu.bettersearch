using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexManager : ILiftiIndexManager
{
    Dictionary<string, IFullTextIndex<string>> _indices = new();

    public IFullTextIndex<string> GetOrCreateIndexAsync(string key)
    {
        if (_indices.TryGetValue(key, out var index))
        {
            return index;
        }
        return CreateIndexAsync(key);
    }

    public IFullTextIndex<string> CreateIndexAsync(string key)  {
        if (_indices.TryGetValue(key, out var index))
        {
          _indices.Remove(key);
        }

        index = new FullTextIndexBuilder<string>().WithObjectTokenization<SearchDocument>(options =>
            options.WithKey(x => x.Id)
                .WithDynamicFields("Properties",
                    c => c.Fields.ToDictionary(
                        x => x.Key,
                        x => string.Join("", x.Value)
                    )
                )
        ).Build();
        ;
        _indices.Add(key, index);
        return index;
    }

    public Task<Result<bool>> ExistsAsync(string name) => Task.FromResult(Result.Ok(_indices.ContainsKey(name)));
    public Task<Result<bool>> DeleteIndexAsync(string name) => Task.FromResult(Result.Ok(_indices.Remove(name)));
}
