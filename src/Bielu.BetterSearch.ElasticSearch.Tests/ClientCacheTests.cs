using Bielu.BetterSearch.Services;
using Shouldly;

namespace Bielu.BetterSearch.ElasticSearch.Tests;

/// <summary>The shared client cache, linked into the Elasticsearch and OpenSearch providers.</summary>
public class ClientCacheTests
{
    [Fact]
    public void GetOrCreate_NameUnusedForLongerThanItsLifetime_IsForgottenAndItsClientDropped()
    {
        var time = new ManualTimeProvider();
        var cache = new ClientCache<string, object>(time);
        var abandoned = cache.GetOrCreate("abandoned", "old", _ => new object());

        time.Advance(ClientCache<string, object>.UnusedNameLifetime + TimeSpan.FromSeconds(1));
        cache.GetOrCreate("people", "new", _ => new object());

        cache.Count.ShouldBe(1);
        cache.GetOrCreate("abandoned", "old", _ => new object()).ShouldNotBeSameAs(abandoned);
    }

    [Fact]
    public void GetOrCreate_NameUsedWithinItsLifetime_KeepsItsClient()
    {
        var time = new ManualTimeProvider();
        var cache = new ClientCache<string, object>(time);
        var places = cache.GetOrCreate("places", "a", _ => new object());

        // Each call starts the lifetime again, so a name used regularly is never forgotten.
        time.Advance(ClientCache<string, object>.UnusedNameLifetime - TimeSpan.FromMinutes(1));
        cache.GetOrCreate("places", "a", _ => new object()).ShouldBeSameAs(places);
        time.Advance(TimeSpan.FromMinutes(2));
        cache.GetOrCreate("people", "b", _ => new object());

        cache.Count.ShouldBe(2);
        cache.GetOrCreate("places", "a", _ => new object()).ShouldBeSameAs(places);
    }

    [Fact]
    public void GetOrCreate_ManyShortLivedNames_KeepsOnlyTheRecentOnes()
    {
        var time = new ManualTimeProvider();
        var cache = new ClientCache<string, object>(time);
        for (var generation = 0; generation < 20; generation++)
        {
            cache.GetOrCreate($"index-{generation}", $"credential-{generation}", _ => new object());
            time.Advance(ClientCache<string, object>.UnusedNameLifetime / 2);
        }

        // Each generation's name lives for two more generations, so a fresh name per rotation keeps at most three clients.
        cache.Count.ShouldBe(3);
    }

    [Fact]
    public void GetOrCreate_SettingRestoredWhileAnotherIndexStillUsesIt_ReusesTheClient()
    {
        var cache = new ClientCache<string, object>(new ManualTimeProvider());
        var shared = cache.GetOrCreate("people", "a", _ => new object());
        cache.GetOrCreate("places", "a", _ => new object());

        cache.GetOrCreate("people", "b", _ => new object());
        var back = cache.GetOrCreate("people", "a", _ => throw new InvalidOperationException("The client should be reused."));

        back.ShouldBeSameAs(shared);
        cache.Count.ShouldBe(1);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan by) => _timestamp += by.Ticks;
    }
}
