# Getting Started

## Install

Add the core package and one provider:

```bash
dotnet add package Bielu.BetterSearch.Core
dotnet add package Bielu.BetterSearch.Lifti           # in-memory
dotnet add package Bielu.BetterSearch.ElasticSearch   # Elasticsearch
dotnet add package Bielu.BetterSearch.OpenSearch      # OpenSearch
```

| Package | Purpose |
|---------|---------|
| `Bielu.BetterSearch.Abstractions` | Query model and service interfaces |
| `Bielu.BetterSearch.Models` | Document and request models |
| `Bielu.BetterSearch.Core` | Dependency injection and the indexing and search services |
| `Bielu.BetterSearch.FluentApi` | Fluent query builder |
| `Bielu.BetterSearch.ElasticSearch` | Elasticsearch provider |
| `Bielu.BetterSearch.OpenSearch` | OpenSearch provider |
| `Bielu.BetterSearch.Lifti` | In-memory provider |

## Register a provider

```csharp
services.AddBetterSearch(search => search.AddLiftiSearch());
```

### OpenSearch

`AddOpenSearch()` reads its settings from `Bielu:BetterSearch:OpenSearch`, so pass the configuration in:

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;

services.AddBetterSearch(configuration, search => search.AddOpenSearch());
```

```json
{
  "Bielu": {
    "BetterSearch": {
      "OpenSearch": {
        "SharedIndexSettings": {
          "ConnectionString": "http://localhost:9200",
          "AuthenticationType": "PASSWORD",
          "AuthenticationDetails": { "Username": "admin", "Password": "..." }
        },
        "Indexes": {
          "people": { "ConnectionString": "http://people-cluster:9200" }
        },
        "MaxResultWindow": 10000
      }
    }
  }
}
```

| Setting | Meaning |
|---------|---------|
| `SharedIndexSettings` | Connection used for every index without its own entry |
| `ConnectionString` | Cluster URL; a missing one throws when the client is created |
| `AuthenticationType` | `NONE`, `PASSWORD` (basic, `Username` and `Password`) or `APIKEY` (`Id` and `ApiKey`) |
| `Indexes` | Per-index settings; an entry replaces the shared settings for that index, taking only the connection string from them when it sets none |
| `MaxResultWindow` | Deepest result a search may page to, 10,000 by default; match it to the indexes' `index.max_result_window` |

You can also set the options in code with `services.Configure<OpenSearchOptions>(...)`. Documents are stored
with their values under `fields`, as with Elasticsearch, and the same queries give the same results; the
[feature matrix](../../FEATURE_MATRIX.md) lists the OpenSearch specifics.

## Index and search

```csharp
await indexing.IndexDocumentAsync(new SearchDocument
{
    Id = "1",
    Index = "people",
    Fields = new Dictionary<string, IList<object>> { ["Name"] = ["Jane Doe"] },
});

var query = new SearchQuery { Index = "people" };
query.Add(Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "ja" });

var result = await search.SearchAsync(query);
```

`result` is a FluentResults `Result`: check `IsSuccess` before reading `Value`, and read `Errors` when a
provider can't run the query. The [feature matrix](../../FEATURE_MATRIX.md) lists what each provider supports.

> [!NOTE]
> Returning a failed `Result` instead of throwing is the contract every provider is moving to, but in this
> beta some paths still throw, such as a provider that doesn't implement an operation yet or invalid
> connection settings. The [roadmap](../../ROADMAP.md) lists known defects.
