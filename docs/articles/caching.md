# Caching search results

Every `SearchAsync` call goes to the engine unless you register a search result cache. The cache is opt-in: without
one, nothing is cached and no cache service is resolved.

When a cache is registered, `SearchServiceAsync` caches each successful search, and `IndexingServiceAsync` evicts an
index's cached searches after every write to it. It works the same for every provider, because it sits in Core and
no provider changes.

## Register a cache

### One instance: the built-in memory cache

`AddMemorySearchCache` caches results in `IMemoryCache` through `MemorySearchResultCache`. It is **single-node
only**: an eviction reaches this process alone, so use it for an application that runs as one instance, and for
tests.

```csharp
builder.Services.AddBetterSearch(search => search
    .AddElasticSearch()
    .AddMemorySearchCache(cache => cache.Duration = TimeSpan.FromSeconds(30)));
```

Two searches with the same key that miss at the same time both reach the engine. A search that started before an
eviction and ends after it is not served from the cache, so it can't bring back the result the eviction removed.

### Several instances: FusionCache

Add the `Bielu.BetterSearch.Caching.FusionCache` package, register [FusionCache](https://github.com/ZiggyCreatures/FusionCache)
with a distributed cache and a backplane, and call `AddFusionSearchCache`:

```csharp
builder.Services.AddFusionCache()
    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
    .WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redis }))
    .WithBackplane(new RedisBackplane(new RedisBackplaneOptions { Configuration = redis }));

builder.Services.AddBetterSearch(search => search
    .AddElasticSearch()
    .AddFusionSearchCache());
```

`AddFusionSearchCache(cacheName)` uses a named cache instead of the default one. The adapter caches through
`GetOrSetAsync` with FusionCache's tags, and evicts with `RemoveByTagAsync`. It turns fail-safe off for search
entries, so a failed search returns its failed `Result` rather than an expired result.

**Configure a backplane when the application runs on more than one node.** The backplane is what carries an
eviction to the local (L1) memory of the other nodes. Without one, an eviction on node A clears A's memory and the
distributed cache, but nodes B and C keep serving their copy from memory until it expires. The tests run two
FusionCache instances that share a distributed cache and a `MemoryBackplane`, and check that a write through node A's
indexing service makes node B's next search reach the engine.

### Your own cache

Implement `ISearchResultCache` and register it with `AddSearchCache<TCache>()`, which adds it as a singleton:

```csharp
public interface ISearchResultCache
{
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        SearchCacheEntryOptions options, CancellationToken cancellationToken = default);

    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
```

An implementation must meet two rules:

- **Eviction reaches every node.** After `RemoveByTagAsync` returns, no node may return an entry with that tag,
  apart from the propagation delay of the implementation's backplane. Clearing this node and a shared distributed
  cache is not enough. An implementation that can't do this has to say so in its own docs, as
  `MemorySearchResultCache` does.
- **A factory that throws caches nothing**, and its exception reaches the caller. That is how a failed or cancelled
  search stays out of the cache.

The cached value is a `CachedSearchResult`: plain data, with each field value's type kept beside its text, so it
round-trips through a distributed cache's serializer. `SearchCacheEntryOptions` holds the entry's `Duration` and
`Tags`.

### Why not `HybridCache`

Microsoft's `HybridCache` removes an entry or a tag from the distributed cache and from the local memory of the node
that made the write, but not from the memory of the other nodes. After a write on node A, nodes B and C would serve
the old result until it expired, which defeats evicting on writes. So `HybridCache` is not the injection point, and
the library ships no adapter for it.

## What is cached

- Only successful results. A failed `Result` is never cached, and neither is a cancelled search.
- The key is built from the provider type, the application's `KeyPart` and the whole query: the index, every
  sub-query with its occurrence, boost and values, paging, sorting, post filters, highlighting, `Properties` and
  `PreviewAt`. Values are written the same way on every machine, whatever its culture, and a string `"1"` and a
  number `1` give different keys. A query holding something with no stable text, such as a delegate, is not cached.
- A result whose hits hold a field value of a type the cached shape can't carry (anything other than a string,
  `bool`, `int`, `long`, `double`, `float`, `decimal`, `DateTime`, `DateTimeOffset`, `DateOnly`, `JsonElement` or
  `null`) is returned as the provider gave it and not cached.
- A search returns the values read back from the cached shape, whether it hit or missed, so the first search and
  the cached ones return the same types. `TranslationWarning` reasons are kept.
- Observers subscribed to `ISearchServiceAsync` are notified of a search's hits on a cache hit too: they observe
  searches, not engine calls.

## Options

```csharp
search.AddMemorySearchCache(cache =>
{
    cache.Enabled = true;                          // every index, unless the index says otherwise
    cache.Duration = TimeSpan.FromSeconds(30);     // the default
    cache.Indexes["products"] = new SearchCacheIndexOptions
    {
        Duration = TimeSpan.FromMinutes(5),
        Tags = ["catalogue"]                       // evict with ISearchResultCache.RemoveByTagAsync("catalogue")
    };
    cache.Indexes["orders"] = new SearchCacheIndexOptions { Enabled = false };
    cache.KeyPart = query => tenant.Current.Id;    // tenant, culture, user permissions
    cache.Skip = query => query.PageSize > 100;    // skip the cache for these searches
});
```

- `Indexes` is looked up ignoring case, even when you assign a dictionary of your own.
- `KeyPart` is how you keep apart callers that must not see each other's results. The library can't know about
  security trimming, so if results depend on the user, put what they depend on in the key part.
- `Skip` runs for every search; when it returns `true`, the search neither reads nor fills the cache.
- A `Duration` of zero or less makes the search return a failed `Result` that names the setting.
- Options are read through `IOptionsMonitor`, so a change applies to the next search.

## What evicts, and what doesn't

Every write through `IIndexingServiceAsync` (`IndexDocumentAsync`, `IndexMultipleDocumentsAsync`,
`RemoveDocumentAsync`, `RemoveDocumentsAsync` and `RemoveAllDocumentsAsync`) evicts the tag
`SearchCacheOptions.IndexTag(index)` once the provider returns, before the observers are notified. The index name
is compared ignoring case. A write that fails still evicts, as it can have changed part of the index, and the
eviction ignores the caller's cancellation, so a write that went through always evicts. A batch evicts each of its
indexes once, after the batch. If the eviction itself throws, it is logged at `Error` and the write still returns
its own `Result`.

The cache can't see:

- writes made directly on a provider (`IIndexingProviderAsync`), including creating or deleting an index, which
  `IIndexingServiceAsync` doesn't offer;
- writes made by another process that doesn't share the cache;
- on Elasticsearch and OpenSearch, the time until the index next refreshes: a search right after the eviction can
  still read the old documents and cache them again ([#102](https://github.com/bielu/bielu.bettersearch/issues/102)).

Those are bounded only by the entry's duration, which is why the default is short. Evict by hand with
`ISearchResultCache.RemoveByTagAsync(SearchCacheOptions.IndexTag(index))` after such a write.

## Lifti

Lifti keeps its indexes in memory already, so caching its searches gains nothing. It is allowed, and behaves as on
the other providers.
