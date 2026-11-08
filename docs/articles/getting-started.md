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
    public async Task RunAsync()
    {
        await indexing.IndexDocumentAsync(new SearchDocument
        {
            Id = "1",
            Index = "people",
            Fields = new Dictionary<string, IList<object>> { ["Name"] = ["Jane Doe"] },
        });

        var query = new SearchQuery { Index = "people", Page = 1, PageSize = 20 };
        query.Add(Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "ja" });

        var result = await search.SearchAsync(query);
        if (result.IsSuccess)
        {
            foreach (var hit in result.Value.Items)
            {
                // hit.Key, hit.Score, ...
            }
        }
    }
}
```

`result` is a FluentResults `Result`: check `IsSuccess` before reading `Value`, and read `Errors` when a
provider can't run the query. `Page` counts from 1. The [feature matrix](feature-matrix.md) compares every
provider side by side.

> [!NOTE]
> Returning a failed `Result` instead of throwing is the contract every provider is moving to, but in this
> beta some paths still throw, such as invalid or missing connection settings. The
> [roadmap](../../ROADMAP.md) lists known defects.

## Run the engines locally

The repository's Aspire AppHost, `src/examples/Bielu.BetterSearch.Examples.AppHost`, starts Elasticsearch,
OpenSearch and Meilisearch in Docker, with [meilisearch-ui](https://github.com/riccox/meilisearch-ui) for
browsing Meilisearch indexes. Run `aspire run` from `src/`, or `dotnet run` the project; the Meilisearch master
key is the `meilisearch-masterKey` parameter shown in the Aspire dashboard.
