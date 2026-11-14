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

`IndexingServiceAsync` checks its arguments before it calls the provider. A `null` document, list or request,
a document with a `null` or empty `Id` or `Index`, a `DeleteDocumentRequest` with a `null` or empty `Id` or
`Type`, and a `DeleteAllDocumentsRequest` with a `null` or empty `IndexName` return a failed `Result` that names
what is missing. A document the validator rejects returns `Document <id> is not valid.`, and a validator that
fails or throws returns `Validation of document <id> failed`, with the validator's errors or the exception as
the cause. Either error holds the document's id in its `id` metadata. Cancellation still throws.

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
  choose a default with `SetDefaultProvider("products")`, or [route indexes](#route-indexes-to-providers), which
  stops the unkeyed search and indexing services from throwing. A default that names no registered key makes
  `AddBetterSearch` throw.
- A key can be used once, and two keys that differ only in case are rejected; resolving by key is
  case-sensitive, as in `Microsoft.Extensions.DependencyInjection`. Each provider can be added once per
  `AddBetterSearch` call: to reach another server of the same engine, use its per-index settings.
- `SetIndexingServiceType<T>()` and `SetSearchServiceType<T>()` apply to every key. The validator of a key is
  the unkeyed `IDocumentValidatorAsync` unless you register one for that key before calling `AddBetterSearch`,
  for example `services.AddKeyedScoped<IDocumentValidatorAsync, ProductValidator>("products")`.

### Route indexes to providers

To keep injecting the unkeyed `ISearchServiceAsync` and `IIndexingServiceAsync` with two or more providers, route
each index to a provider key, in code or in the `Bielu:BetterSearch:Routes` configuration section:

```csharp
builder.Services.AddBetterSearch(builder.Configuration, search => search
    .AddElasticSearch(key: "products")
    .AddMeilisearch(key: "site")
    .RouteIndex("products", "products")
    .SetDefaultProvider("site"));
```

```json
{ "Bielu": { "BetterSearch": { "Routes": { "products": "products", "pages": "site" } } } }
```

- With at least one route, the unkeyed services send each call to the keyed service of the provider routed for
  its index: `SearchQuery.Index`, `SearchDocument.Index`, `DeleteDocumentRequest.Type` (which holds the index
  name), the `index` of `RemoveDocumentsAsync`, `DeleteAllDocumentsRequest.IndexName`, and for
  `IIndexLifecycleServiceAsync` the index being rebuilt (`IndexRebuild.Index`). Index names compare
  ignoring case, and so do the keys a route names.
- An index with no route goes to the default provider: the one `SetDefaultProvider` names, or the only one. With
  no default, the call returns a failed `Result` that names the index and the registered keys; resolving the
  unkeyed services doesn't throw.
- A route that names a key no provider is registered under, or a configured route with an empty value, makes
  `AddBetterSearch` throw. A `RouteIndex` call replaces a configured route for the same index, and a later
  `RouteIndex` call for an index replaces an earlier one.
- `IndexMultipleDocumentsAsync` checks every document for null values and a provider, and validates each one with
  the validator of its provider, before indexing any of them. So a document whose index has no route when there is
  no default provider, or that its provider's validator rejects, fails the whole batch with nothing stored, as
  with one provider. It then splits the documents by provider and calls each provider's indexing service once.
  When one fails or throws, the others are still called, and the failed `Result` has one error saying how many
  documents each provider indexed. As for a single provider, its `ids` metadata holds the ids not confirmed as
  stored, and its reasons have one error for each provider that failed, with the key in `key` and that
  provider's ids not confirmed as stored in `ids`. A provider whose error has no `ids` metadata, as when it threw,
  counts as having stored none of its documents. Cancellation is not caught: it stops the batch, and the
  providers already called keep what they stored.
- Routing applies to the unkeyed `ISearchServiceAsync` and `IIndexingServiceAsync` only. The unkeyed
  `ISearchProviderAsync` and `IIndexingProviderAsync` still use the default provider, or throw on resolution
  when there is none.

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

## Run code after a write

The services have no events. To run code around an operation, such as logging every document indexed, decorate
the service in DI, as the [search cache](caching.md) does. Register the decorator after `AddBetterSearch`, for
example with Scrutor's `Decorate`, which wraps the unkeyed service; with routed indexes that is the routing
service, so it sees every call made through the unkeyed service, whichever provider it goes to. A decorator sees
the `Result` of each call, so it can act on success only. Calls made on a keyed service, or on a provider directly,
don't go through it.

```csharp
builder.Services.AddBetterSearch(builder.Configuration, search => search.AddElasticSearch());
builder.Services.Decorate<IIndexingServiceAsync, LoggingIndexingService>();

sealed class LoggingIndexingService(IIndexingServiceAsync inner, ILogger<LoggingIndexingService> logger)
    : IIndexingServiceAsync
{
    public async Task<Result> IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        var result = await inner.IndexDocumentAsync(document, cancellationToken);
        if (result.IsSuccess)
        {
            logger.LogInformation("Indexed {Id}", document.Id);
        }

        return result;
    }

    public Task<Result<int>> IndexMultipleDocumentsAsync(IEnumerable<SearchDocument> documents, CancellationToken cancellationToken = default) =>
        inner.IndexMultipleDocumentsAsync(documents, cancellationToken);

    public Task<Result> RemoveDocumentAsync(DeleteDocumentRequest document, CancellationToken cancellationToken = default) =>
        inner.RemoveDocumentAsync(document, cancellationToken);

    public Task<Result<int>> RemoveDocumentsAsync(string index, IEnumerable<string> ids, CancellationToken cancellationToken = default) =>
        inner.RemoveDocumentsAsync(index, ids, cancellationToken);

    public Task<Result<long?>> RemoveAllDocumentsAsync(DeleteAllDocumentsRequest request, CancellationToken cancellationToken = default) =>
        inner.RemoveAllDocumentsAsync(request, cancellationToken);

    public Task<Result<bool>> UpdateDocumentAsync(string index, string id, IDictionary<string, IList<object>> fields,
        CancellationToken cancellationToken = default) =>
        inner.UpdateDocumentAsync(index, id, fields, cancellationToken);
}
```

For metrics and traces, use the built-in [OpenTelemetry instrumentation](feature-matrix.md#observability)
instead.

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
