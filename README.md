# Bielu.BetterSearch

[![CI](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Docs](https://img.shields.io/badge/docs-search.bielu.pl-blue)](https://search.bielu.pl)

A provider-agnostic search abstraction for .NET: describe a query once, then run it against whichever
search engine you configure.

> **Status: beta, not production ready.** The public API will change before 1.0. See [ROADMAP.md](./ROADMAP.md) and
> [FEATURE_MATRIX.md](./FEATURE_MATRIX.md).

## Packages

| Package | Purpose |
|---------|---------|
| `Bielu.BetterSearch.Abstractions` | Query model and service interfaces |
| `Bielu.BetterSearch.Models` | Document and request models |
| `Bielu.BetterSearch.Core` | Dependency injection and the indexing and search services |
| `Bielu.BetterSearch.FluentApi` | Fluent query builder |
| `Bielu.BetterSearch.ElasticSearch` | Elasticsearch provider |
| `Bielu.BetterSearch.Meilisearch` | Meilisearch provider |
| `Bielu.BetterSearch.Lifti` | In-memory provider, due to be replaced (see the roadmap) |

## Usage

```csharp
services.AddBetterSearch(search => search.AddLiftiSearch());
// or, configured from "Bielu:BetterSearch:ElasticSearch":
services.AddBetterSearch(search => search.AddElasticSearch());
```

To use Meilisearch, register its provider and point it at a server, in code or under
`Bielu:BetterSearch:Meilisearch` in configuration:

```csharp
services.AddBetterSearch(configuration, search => search.AddMeilisearch());
services.Configure<MeilisearchOptions>(options => options.SharedIndexSettings = new MeilisearchIndexSettings
{
    ConnectionString = "http://localhost:7700",
    ApiKey = "...", // the master key or an API key; leave empty when the server has none
});
```

```csharp
await indexing.IndexDocumentAsync(new SearchDocument
{
    Id = "1",
    Index = "people",
    Fields = new Dictionary<string, IList<object>> { ["Name"] = ["Jane Doe"] },
});

var query = new SearchQuery { Index = "people", Page = 1, PageSize = 20 };
query.Add(Occurance.MUST, new PrefixSubQuery { Field = "Name", Value = "ja" });

var result = await search.SearchAsync(query);
```

## Running the search engines locally

`src/examples/Bielu.BetterSearch.Examples.AppHost` is an Aspire AppHost that runs Elasticsearch and
Meilisearch in Docker (`aspire run` from `src/`, or `dotnet run` the project). For Meilisearch the
Aspire dashboard links to Meilisearch's own search preview and to
[meilisearch-ui](https://github.com/riccox/meilisearch-ui), where you can browse indexes and documents
and try queries. The master key is the `meilisearch-masterKey` parameter shown in the dashboard.

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md). Licensed under [MIT](./LICENSE).
