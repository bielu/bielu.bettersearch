# Getting Started

## Install

The packages are in beta, so install them with `--prerelease`. Add the core package and one provider:

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease

# Then one provider:
dotnet add package Bielu.BetterSearch.ElasticSearch --prerelease   # Elasticsearch
dotnet add package Bielu.BetterSearch.OpenSearch --prerelease      # OpenSearch
dotnet add package Bielu.BetterSearch.Meilisearch --prerelease     # Meilisearch
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
| `Bielu.BetterSearch.Lifti` | [In-memory provider](providers/lifti.md) |

## Register a provider

`AddBetterSearch` registers `IIndexingServiceAsync` and `ISearchServiceAsync`, backed by the provider you
pick. The quickest start is the in-memory provider, which needs no server:

```csharp
using Bielu.BetterSearch.DepedencyInjection;

builder.Services.AddBetterSearch(search => search.AddLiftiSearch());
```

The Elasticsearch, OpenSearch and Meilisearch providers read their settings from configuration, so use the
overload that takes it:

```csharp
builder.Services.AddBetterSearch(builder.Configuration, search => search.AddElasticSearch());
```

Each provider has its own page with its settings and, importantly, **what it doesn't support**:

| Provider | Register with | Configuration section |
|----------|---------------|-----------------------|
| [Elasticsearch](providers/elasticsearch.md) | `AddElasticSearch()` | `Bielu:BetterSearch:ElasticSearch` |
| [OpenSearch](providers/opensearch.md) | `AddOpenSearch()` | `Bielu:BetterSearch:OpenSearch` |
| [Meilisearch](providers/meilisearch.md) | `AddMeilisearch()` | `Bielu:BetterSearch:Meilisearch` |
| [Lifti](providers/lifti.md) | `AddLiftiSearch()` | none |

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
the `Score` the engine gave it. `Page` counts from 1. A cancelled search throws `OperationCanceledException`
rather than returning a failed `Result`. The [feature matrix](feature-matrix.md) compares every provider side
by side.

## Watch indexing

`IIndexingServiceAsync` is an `IObservable` of `SearchDocument`, `DeleteDocumentRequest` and
`DeleteAllDocumentsRequest`. All three streams follow one contract:

- `OnNext` is sent once for each operation that succeeded, after the provider has done it: a document indexed,
  a document removed, an index emptied.
- A document the validator rejects, or an operation the provider fails, sends nothing. The returned `Result`
  carries the reason.
- `OnError` is never sent. `OnCompleted` is sent once, when the service is disposed, so a stream stays open for
  every operation the service performs. Dispose the value `Subscribe` returns to stop earlier.

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

> [!NOTE]
> Returning a failed `Result` instead of throwing is the contract every provider is moving to, but in this
> beta some paths still throw, such as invalid or missing connection settings. The
> [roadmap](../../ROADMAP.md) lists known defects.

## Run the engines locally

The repository's Aspire AppHost, `src/examples/Bielu.BetterSearch.Examples.AppHost`, starts Elasticsearch,
OpenSearch and Meilisearch in Docker, with [meilisearch-ui](https://github.com/riccox/meilisearch-ui) for
browsing Meilisearch indexes. Run `aspire run` from `src/`, or `dotnet run` the project; the Meilisearch master
key is the `meilisearch-masterKey` parameter shown in the Aspire dashboard.
