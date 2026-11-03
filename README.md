# Bielu.BetterSearch

[![CI](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

A provider-agnostic search abstraction for .NET: describe a query once, then run it against whichever
search engine you configure.

> **Status: beta, not production ready.** The public API will change before 1.0, and the Elasticsearch
> and OpenSearch providers can index documents but cannot search yet. See [ROADMAP.md](./ROADMAP.md) and
> [FEATURE_MATRIX.md](./FEATURE_MATRIX.md).

## Packages

| Package | Purpose |
|---------|---------|
| `Bielu.BetterSearch.Abstractions` | Query model and service interfaces |
| `Bielu.BetterSearch.Models` | Document and request models |
| `Bielu.BetterSearch.Core` | Dependency injection and the indexing and search services |
| `Bielu.BetterSearch.FluentApi` | Fluent query builder |
| `Bielu.BetterSearch.ElasticSearch` | Elasticsearch provider (indexing only so far) |
| `Bielu.BetterSearch.OpenSearch` | OpenSearch provider (indexing only so far) |
| `Bielu.BetterSearch.Lifti` | In-memory provider, due to be replaced (see the roadmap) |

## Usage

```csharp
services.AddBetterSearch(search => search.AddLiftiSearch());
```

To use OpenSearch, register its provider and point it at a cluster, in code or under
`Bielu:BetterSearch:OpenSearch` in configuration:

```csharp
services.AddBetterSearch(configuration, search => search.AddOpenSearch());
services.Configure<OpenSearchOptions>(options => options.SharedIndexSettings = new OpenSearchIndexSettings
{
    ConnectionString = "http://localhost:9200",
    AuthenticationType = OpenSearchAuthenticationType.PASSWORD, // NONE, PASSWORD or APIKEY
    AuthenticationDetails = new OpenSearchAuthenticationDetails { Username = "admin", Password = "..." },
});
```

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

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md). Licensed under [MIT](./LICENSE).
