using System.Collections.Concurrent;
using Bielu.BetterSearch.Abstractions.Query;
using FluentResults;
using Lifti;

namespace Bielu.BetterSearch.Lifti.Services;

public class LiftiIndexManager : ILiftiIndexManager
{
    private readonly ConcurrentDictionary<string, IFullTextIndex<string>> _indices = new();

    public async Task<Result<IFullTextIndex<string>>> GetOrCreateIndexAsync(string key)
    {
        if (_indices.TryGetValue(key, out var index))
        {
            return Result.Ok(index);
        }
        return await CreateIndexAsync(key).ConfigureAwait(false);
    }

    public Task<Result<IFullTextIndex<string>?>> CreateIndexAsync(string key)
    {
        var index = new FullTextIndexBuilder<string>().WithObjectTokenization<SearchDocument>(options =>
            options.WithKey(x => x.Id)
                .WithDynamicFields("Fields",
                    c => c.Fields.ToDictionary(
                        x => x.Key,
                        x => string.Join(" ", (x.Value ?? []).Select(QueryValueText.From))
                    )
                )
        ).Build();

        _indices[key] = index;
        return Task.FromResult(Result.Ok<IFullTextIndex<string>?>(index));
    }

    public Task<Result<IFullTextIndex<string>?>> GetIndexAsync(string key) =>
        Task.FromResult(Result.Ok(_indices.TryGetValue(key, out var index) ? index : null));

    public Task<Result<bool>> ExistsAsync(string name) => Task.FromResult(Result.Ok(_indices.ContainsKey(name)));

    public Task<Result<bool>> DeleteIndexAsync(string name) => Task.FromResult(Result.Ok(_indices.TryRemove(name, out _)));
}
