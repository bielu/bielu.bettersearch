# Caching search results

Every `SearchAsync` call goes to the engine unless you register a search result cache. The cache is opt-in: without
one, nothing is cached and no cache service is resolved.

When a cache is registered, `AddBetterSearch` decorates every `ISearchServiceAsync` and `IIndexingServiceAsync`
registration, keyed or not, with [Scrutor](https://github.com/khellang/Scrutor): the search decorator caches each
successful search, and the indexing decorator evicts an index's cached searches after every write to it. It wraps
whatever service type is registered, including one set with `SetSearchServiceType<T>()` or
`SetIndexingServiceType<T>()`, and it works the same for every provider, because the decorators sit in Core and no
provider changes. Only the registrations that exist when `AddBetterSearch` runs are decorated, along with keyed
registrations under a string key; a decorator you add afterwards, with Scrutor's `Decorate` or otherwise, wraps the
cache decorators.

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
Each entry has a size of 1, so an `IMemoryCache` with a `SizeLimit` holds that many searches. The cache keeps a
small record for each tag only while a search with that tag runs or an entry with it is stored, so searching many
short-lived indexes doesn't grow it past the cached entries.

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

`SearchCacheEntryOptions` holds the entry's `Duration` and `Tags`. The cached value's type is internal to Core, so
treat `T` as opaque: store it and return it. It is plain data, with public properties, a parameterless constructor and
each field value's type kept beside its text, so a reflection-based serializer such as FusionCache's
`FusionCacheSystemTextJsonSerializer` round-trips it; the tests pin that. A serializer that only handles types it was
given ahead of time, such as a source-generated `System.Text.Json` context, can't serialize it.

### Why not `HybridCache`

Microsoft's `HybridCache` removes an entry or a tag from the distributed cache and from the local memory of the node
that made the write, but not from the memory of the other nodes. After a write on node A, nodes B and C would serve
the old result until it expired, which defeats evicting on writes. So `HybridCache` is not the injection point, and
the library ships no adapter for it.

## What is cached

- Only successful results. A failed `Result` is never cached, and neither is a cancelled search.
- The key is built from the provider key, the application's `KeyPart` and the whole query: the index, every
  sub-query with its occurrence, boost and values, paging, sorting, post filters, highlighting, `Properties` and
  `PreviewAt`. Values are written the same way on every machine, whatever its culture, and a string `"1"` and a
  number `1` give different keys. A `JsonElement` or `JsonNode` value is keyed by its JSON text. A query holding
  something with no stable text is not cached: a delegate, or a value of any other type that is not a scalar, a
  string, an enum, a collection, a tuple or part of the query model (a type in Abstractions, or one that implements
  an Abstractions interface such as `ISearchSubQuery`).
- A result whose hits hold a field value of a type the cached shape can't carry (anything other than a string,
  `bool`, `int`, `long`, `double`, `float`, `decimal`, `DateTime`, `DateTimeOffset`, `DateOnly`, `JsonElement` or
  `null`) is returned as the provider gave it and not cached.
- A search returns the values read back from the cached shape, whether it hit or missed, so the first search and
  the cached ones return the same types. `TranslationWarning` reasons are kept.
- If the cache throws before the search runs, for example because its store is down, the search runs without it. If
  it throws while storing a successful search, that search is returned uncached and the engine is not called again.
  Both are logged as warnings.
- Observers subscribed to `ISearchServiceAsync` are notified of a search's hits on a cache hit too. The decorator
  has its own observers: `Subscribe` registers on it, and it notifies them from the result it returns, on a hit and on
  a miss, so they observe searches, not engine calls. The decorated service's own observers are not used.
- A cache hit is not in the [telemetry](feature-matrix.md#observability): it returns before `SearchServiceAsync`
  runs, so it has no `search` span and no `bettersearch.operations` count. Only searches that reach the service, the
  misses and the searches that aren't cached, are recorded.
- A keyed search service's entries are keyed by its provider key, so two providers that both have an index called
  `products` never share an entry. The unkeyed service uses the default provider's key, so it shares entries with
  that provider's keyed service.

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

Every write on the decorated `IIndexingServiceAsync` (`IndexDocumentAsync`, `IndexMultipleDocumentsAsync`,
`RemoveDocumentAsync`, `RemoveDocumentsAsync`, `RemoveAllDocumentsAsync` and `UpdateDocumentAsync`) evicts the tag
`SearchCacheOptions.IndexTag(index)` once the decorated service returns. The index name is compared ignoring case.
A write that fails still evicts, as it can have changed part of the index, and the eviction ignores the caller's
cancellation, so a write that went through always evicts. A batch evicts each of its indexes once, after the one
call to the decorated service. If the eviction itself throws, it is logged at `Error` and the write still returns its
own `Result`. `CompleteRebuildAsync` on the decorated `IIndexLifecycleServiceAsync` evicts the rebuilt index the same way, as the
swap changes what its searches return; starting, filling and aborting a rebuild don't evict. `GetDocumentAsync` and `CountAsync` on the decorated `ISearchServiceAsync` are not cached:
each call goes to the decorated service.

The indexing decorator has its own observers too: `Subscribe` registers on it, and it notifies them after the
eviction, following the `IIndexingServiceAsync` contract (once for each document indexed, each distinct id removed,
each index emptied, and nothing when the write fails). So an observer that searches the index from `OnNext` reads past
the evicted result. The decorated service's own observers are not used.

The cache can't see:

- writes made on an `IIndexingProviderAsync` directly, rather than through `IIndexingServiceAsync`, including
  `CreateIndexAsync` and `DeleteIndexAsync`, which the service doesn't have;
- writes through an indexing service that didn't come from the container, or that was registered after
  `AddBetterSearch` ran;
- writes made by another process that doesn't share the cache;
- on Elasticsearch and OpenSearch, the time until the index next refreshes: a search right after the eviction can
  still read the old documents and cache them again ([#102](https://github.com/bielu/bielu.bettersearch/issues/102)).
  Set the index's `WriteVisibility` to `WaitUntilSearchable` to close that window: a write then returns, and evicts,
  only once a search can see it. See When writes are searchable for
  [Elasticsearch](providers/elasticsearch.md#when-writes-are-searchable) and
  [OpenSearch](providers/opensearch.md#when-writes-are-searchable).

Those are bounded only by the entry's duration, which is why the default is short. Evict by hand with
`ISearchResultCache.RemoveByTagAsync(SearchCacheOptions.IndexTag(index))` after such a write.

## Lifti

Lifti keeps its indexes in memory, so a cached search saves no network call. It still saves translating and running
the query and mapping its results, which can matter for a repeated, expensive query; measure before you turn it on.
It is allowed, and behaves as on the other providers.
