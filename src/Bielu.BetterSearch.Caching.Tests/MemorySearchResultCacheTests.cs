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

    [Fact]
    public async Task GetOrCreateAsync_FactoryThrowsForManyTags_HoldsNoTags()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemorySearchResultCache(memory);

        //Act
        for (var i = 0; i < 100; i++)
        {
            var options = new SearchCacheEntryOptions { Duration = TimeSpan.FromMinutes(1), Tags = [$"index-{i}"] };
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await cache.GetOrCreateAsync<int>($"key-{i}", _ => throw new InvalidOperationException("no"), options, Token));
        }

        //Assert
        cache.TagCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetOrCreateAsync_EntriesLeaveTheCache_ReleasesTheirTags()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemorySearchResultCache(memory);
        for (var i = 0; i < 100; i++)
        {
            var options = new SearchCacheEntryOptions { Duration = TimeSpan.FromMinutes(1), Tags = [$"index-{i}", "shared"] };
            await cache.GetOrCreateAsync($"key-{i}", _ => ValueTask.FromResult(i), options, Token);
        }

        var whileCached = cache.TagCount;

        //Act
        // Compact evicts every entry; MemoryCache runs the eviction callbacks on the thread pool.
        memory.Compact(1.0);
        var released = SpinWait.SpinUntil(() => cache.TagCount == 0, TimeSpan.FromSeconds(10));

        //Assert
        whileCached.ShouldBe(101);
        released.ShouldBeTrue($"{cache.TagCount} tags are still held");
    }

    [Fact]
    public async Task RemoveByTagAsync_AfterAnotherEntryWithTheTagLeft_StillEvictsTheCachedOne()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemorySearchResultCache(memory);
        await cache.GetOrCreateAsync("first", _ => ValueTask.FromResult(1), Tagged, Token);
        await cache.GetOrCreateAsync("second", _ => ValueTask.FromResult(2), Tagged, Token);
        memory.Remove("first");

        //Act
        await cache.RemoveByTagAsync("a", Token);
        var second = await cache.GetOrCreateAsync("second", _ => ValueTask.FromResult(3), Tagged, Token);

        //Assert
        second.ShouldBe(3);
    }

    [Fact]
    public async Task GetOrCreateAsync_MemoryCacheHasSizeLimit_CachesTheResult()
    {
        //Arrange
        using var memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 });
        var cache = new MemorySearchResultCache(memory);

        //Act
        var first = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(1), Tagged, Token);
        var second = await cache.GetOrCreateAsync("key", _ => ValueTask.FromResult(2), Tagged, Token);

        //Assert
        first.ShouldBe(1);
        second.ShouldBe(1);
    }
}
