using Bielu.BetterSearch.Abstractions.Caching;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace Bielu.BetterSearch.Caching.Tests;

public class MemorySearchResultCacheTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly SearchCacheEntryOptions Tagged = new() { Duration = TimeSpan.FromMinutes(1), Tags = ["a"] };

    [Fact]
    public async Task GetOrCreateAsync_RemovedByTagWhileFactoryRuns_DoesNotServeTheResult()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemorySearchResultCache(memory);

        //Act
        var first = await cache.GetOrCreateAsync("key", async _ =>
        {
            await cache.RemoveByTagAsync("a", Token);
            return 1;
        }, Tagged, Token);
        var second = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(2), Tagged, Token);

        //Assert
        first.ShouldBe(1);
        second.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveByTagAsync_OtherTag_KeepsEntry()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemorySearchResultCache(memory);
        await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(1), Tagged, Token);

        //Act
        await cache.RemoveByTagAsync("b", Token);
        var kept = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(2), Tagged, Token);
        await cache.RemoveByTagAsync("a", Token);
        var evicted = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(3), Tagged, Token);

        //Assert
        kept.ShouldBe(1);
        evicted.ShouldBe(3);
    }

    [Fact]
    public async Task GetOrCreateAsync_FactoryThrows_CachesNothing()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemorySearchResultCache(memory);

        //Act
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await cache.GetOrCreateAsync<int>("key", _ => throw new InvalidOperationException("no"), Tagged, Token));
        var value = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(2), Tagged, Token);

        //Assert
        value.ShouldBe(2);
    }
}
