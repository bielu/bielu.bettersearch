# Getting Started

## Install

The packages are in beta, so install them with `--prerelease`. Add the core package and one provider:

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease

# Then one provider:
dotnet add package Bielu.BetterSearch.ElasticSearch --prerelease   # Elasticsearch
dotnet add package Bielu.BetterSearch.OpenSearch --prerelease      # OpenSearch
dotnet add package Bielu.BetterSearch.Meilisearch --prerelease     # Meilisearch
dotnet add package Bielu.BetterSearch.Typesense --prerelease       # Typesense
dotnet add package Bielu.BetterSearch.Lifti --prerelease           # in-memory
```

| Package | Purpose |
|---------|---------|
| `Bielu.BetterSearch.Abstractions` | Query model (`SearchQuery` and its sub-queries) and service interfaces |
| `Bielu.BetterSearch.Models` | `SearchDocument` and the request models |
| `Bielu.BetterSearch.Core` | `AddBetterSearch` and the indexing and search services |
| `Bielu.BetterSearch.FluentApi` | Fluent query builder |
| `Bielu.BetterSearch.ElasticSearch` | [Elasticsearch provider](providers/elasticsearch.md) |
| `Bielu.BetterSearch.OpenSearch` | [OpenSearch provider](providers/opensearch.md) |
| `Bielu.BetterSearch.Meilisearch` | [Meilisearch provider](providers/meilisearch.md) |
| `Bielu.BetterSearch.Typesense` | [Typesense provider](providers/typesense.md) |
| `Bielu.BetterSearch.Lifti` | [In-memory provider](providers/lifti.md) |

## Register a provider

`AddBetterSearch` registers `IIndexingServiceAsync` and `ISearchServiceAsync`, backed by the provider you
pick. The quickest start is the in-memory provider, which needs no server:

```csharp
using Bielu.BetterSearch.DepedencyInjection;

builder.Services.AddBetterSearch(search => search.AddLiftiSearch());
```

The Elasticsearch, OpenSearch, Meilisearch and Typesense providers read their settings from configuration, so use the
overload that takes it:

```csharp
builder.Services.AddBetterSearch(builder.Configuration, search => search.AddElasticSearch());
```

`AddBetterSearch` registers the validator, the indexing and search services and the provider once, after
your callback has run, so a `SetIndexingServiceType<T>()`, `SetSearchServiceType<T>()` or
`SetDocumentValidatorType<T>()` call works before or after the provider extension. It uses `TryAdd`, so a
service you registered before calling `AddBetterSearch` is kept. Each call has its own configuration, so
two containers in one process (an app and a test host, say) don't share provider types. Without a provider
extension, every call that reaches the provider returns a failed `Result` saying that no provider is
registered. Calls that the services answer without the provider don't: an empty batch to
`IndexMultipleDocumentsAsync` or an empty id list to `RemoveDocumentsAsync` returns `Ok(0)`, and a document
with a null field or one that fails validation returns that error instead.

Each provider has its own page with its settings and, importantly, **what it doesn't support**:

| Provider | Register with | Configuration section |
|----------|---------------|-----------------------|
| [Elasticsearch](providers/elasticsearch.md) | `AddElasticSearch()` | `Bielu:BetterSearch:ElasticSearch` |
| [OpenSearch](providers/opensearch.md) | `AddOpenSearch()` | `Bielu:BetterSearch:OpenSearch` |
| [Meilisearch](providers/meilisearch.md) | `AddMeilisearch()` | `Bielu:BetterSearch:Meilisearch` |
| [Typesense](providers/typesense.md) | `AddTypesense()` | `Bielu:BetterSearch:Typesense` |
| [Lifti](providers/lifti.md) | `AddLiftiSearch()` | none |

## Use more than one provider

Each provider extension registers its provider under a key, its name by default (`ElasticSearch`,
`OpenSearch`, `Meilisearch`, `Typesense`, `Lifti`, also in each extension class's `DefaultKey`), or the key you pass. For each
key, `AddBetterSearch` registers keyed services: `ISearchProviderAsync`, `IIndexingProviderAsync`,
`ISearchServiceAsync`, `IIndexingServiceAsync` and `IDocumentValidatorAsync`. So one container can hold several
engines, and you pick one with `[FromKeyedServices]`:

```csharp
builder.Services.AddBetterSearch(builder.Configuration, search => search
    .AddElasticSearch(key: "products")
    .AddMeilisearch(key: "site"));

public class ProductSearch([FromKeyedServices("products")] ISearchServiceAsync search) { }
```

- With one provider, the unkeyed services use it, as before.
- With two or more, resolving an unkeyed `ISearchServiceAsync`, `IIndexingServiceAsync`, `ISearchProviderAsync`
  or `IIndexingProviderAsync` throws an `InvalidOperationException` that lists the registered keys, unless you
  choose a default with `SetDefaultProvider("products")`. A default that names no registered key makes
  `AddBetterSearch` throw.
- A key can be used once, and two keys that differ only in case are rejected; resolving by key is
  case-sensitive, as in `Microsoft.Extensions.DependencyInjection`. Each provider can be added once per
  `AddBetterSearch` call: to reach another server of the same engine, use its per-index settings.
- `SetIndexingServiceType<T>()` and `SetSearchServiceType<T>()` apply to every key. The validator of a key is
  the unkeyed `IDocumentValidatorAsync` unless you register one for that key before calling `AddBetterSearch`,
  for example `services.AddKeyedScoped<IDocumentValidatorAsync, ProductValidator>("products")`.
- Observers are per key: each keyed service is its own instance, so an observer subscribed to the `products`
  indexing service hears nothing indexed through `site`. The unkeyed service is a separate instance too, even
  when it uses the same provider, so it doesn't hear operations run through the keyed one.
- Routing an index to an engine by its name is not supported yet ([#122](https://github.com/bielu/bielu.bettersearch/issues/122)); resolve the service for the engine you want.

## Index and search

Inject `IIndexingServiceAsync` and `ISearchServiceAsync`:

```csharp
using Bielu.BetterSearch;
using Bielu.BetterSearch.Abstractions.Query;
using Bielu.BetterSearch.Abstractions.Query.SubQueries;
using Bielu.BetterSearch.Abstractions.Services;

public class PeopleSearch(IIndexingServiceAsync indexing, ISearchServiceAsync search)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await indexing.IndexDocumentAsync(new SearchDocument
        {
            Id = "1",
            Index = "people",
            Fields = new Dictionary<string, IList<object>> { ["Name"] = ["Jane Doe"] },
        }, cancellationToken);

        var query = new SearchQuery { Index = "people", Page = 1, PageSize = 20 };
        query.Add(Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "ja" });

        var result = await search.SearchAsync(query, cancellationToken);
        if (result.IsSuccess)
        {
            foreach (var hit in result.Value.Items)
            {
                // hit.Document.Id, hit.Document.CustomProperties, hit.Score, ...
            }
        }
    }
}
```

`result` is a FluentResults `Result`: check `IsSuccess` before reading `Value`, and read `Errors` when a
provider can't run the query. Each item is an `ISearchHit`: the stored `Document`, with its id unchanged, and
the `Score` the engine gave it, and the `Highlights` that `HighlightQueries` asked for, on providers that
highlight. `Page` counts from 1. A cancelled search throws `OperationCanceledException`
rather than returning a failed `Result`. The [feature matrix](feature-matrix.md) compares every provider side
by side.

## Watch indexing

`IIndexingServiceAsync` is an `IObservable` of `SearchDocument`, `DeleteDocumentRequest` and
`DeleteAllDocumentsRequest`. All three streams follow one contract:

- `OnNext` is sent once for each operation that succeeded, after the provider has done it: a document indexed,
  a document removed, an index emptied. `RemoveDocumentsAsync` sends one `DeleteDocumentRequest` for each
  distinct id when it succeeds.
- A document the validator rejects, or an operation the provider fails, sends nothing. The returned `Result`
  carries the reason.
- `OnError` is never sent. `OnCompleted` is sent once, when the service is disposed, so a stream stays open for
  every operation the service performs. Dispose the value `Subscribe` returns to stop earlier. Subscribing after
  the service is disposed sends `OnCompleted` straight away, and operations after it notify nobody.
- An observer that throws is logged and skipped: the operation still returns its `Result`, and the other
  observers are still notified. `Dispose` never throws.
- Subscribing the same observer twice notifies it once, until both handles `Subscribe` returned are disposed.

`ISearchServiceAsync` follows the same contract for `ISearchModel`: `OnNext` for each hit of a successful search.
Both interfaces extend `IDisposable`. The DI scope owns the services and disposes them, which completes the
streams for every subscriber in that scope, so don't dispose an injected service yourself: dispose the handle
`Subscribe` returned to stop listening. Only code that creates a service itself, outside DI, should dispose it.

Observers are called one at a time, and never after `OnCompleted`, only while the service isn't used
concurrently. When one instance runs operations in parallel, for example
`Task.WhenAll(documents.Select(indexing.IndexDocumentAsync))`, or is disposed while an operation runs, an observer
can get overlapping `OnNext` calls, or one last `OnNext` after `OnCompleted`. In that case the observer must be
thread-safe.

```csharp
using var subscription = indexing.Subscribe(new IndexedLogger());

sealed class IndexedLogger : IObserver<SearchDocument>
{
    public void OnNext(SearchDocument document) => Console.WriteLine($"Indexed {document.Id}");
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
```

## Filter by culture

To search one culture, index it in the plain `Culture` field and filter on it with the fluent API
(`Bielu.BetterSearch.FluentApi`):

```csharp
using Bielu.BetterSearch.Abstractions.Fields;
using Bielu.BetterSearch.FluentApi;
using Bielu.BetterSearch.FluentApi.Fluent.Extensions;

document.Fields[CultureField.Name] = [CultureField.Value("en-US")];

var query = FluentApiSearchQueryCreator.Create()
    .WithSearchQuery(search => search.WithIndex("pages"))
    .CreateSearchQuery(search => search
        .CreateTermQuery(term => term.WithField("Title").WithValue("welcome"))
        .FilterByCulture("en-US"));
```

On Lifti the field is ignored, so a document holding the culture value in another field matches too.

Each clause takes the occurrence of the query or bool it is created on, `MUST` unless you choose another. Call
`Must()`, `Should()`, `MustNot()` or `Filter()` on the sub-query itself to choose its own:

```csharp
.CreateBoolQuery(cities => cities
    .CreateTermQuery(term => term.WithField("City").WithValue("Warsaw").Should())
    .CreateTermQuery(term => term.WithField("City").WithValue("Krakow").Should()))
```

> [!NOTE]
> Returning a failed `Result` instead of throwing is the contract every provider is moving to, but in this
> beta some paths still throw, such as invalid or missing connection settings. The
> [roadmap](../../ROADMAP.md) lists known defects.

## Run the engines locally

The repository's Aspire AppHost, `src/examples/Bielu.BetterSearch.Examples.AppHost`, starts Elasticsearch,
OpenSearch and Meilisearch in Docker, with [meilisearch-ui](https://github.com/riccox/meilisearch-ui) for
browsing Meilisearch indexes. Run `aspire run` from `src/`, or `dotnet run` the project; the Meilisearch master
key is the `meilisearch-masterKey` parameter shown in the Aspire dashboard.
