# Getting Started

## Install

Add the core package and one provider:

```bash
dotnet add package Bielu.BetterSearch.Core
dotnet add package Bielu.BetterSearch.Lifti           # in-memory
dotnet add package Bielu.BetterSearch.ElasticSearch   # Elasticsearch
```

| Package | Purpose |
|---------|---------|
| `Bielu.BetterSearch.Abstractions` | Query model and service interfaces |
| `Bielu.BetterSearch.Models` | Document and request models |
| `Bielu.BetterSearch.Core` | Dependency injection and the indexing and search services |
| `Bielu.BetterSearch.FluentApi` | Fluent query builder |
| `Bielu.BetterSearch.ElasticSearch` | Elasticsearch provider |
| `Bielu.BetterSearch.Lifti` | In-memory provider |

## Register a provider

```csharp
services.AddBetterSearch(search => search.AddLiftiSearch());
```

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
