# Bielu.BetterSearch

[![CI](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Bielu.BetterSearch.Core.svg)](https://www.nuget.org/packages/Bielu.BetterSearch.Core/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bielu.BetterSearch.Core.svg)](https://www.nuget.org/packages/Bielu.BetterSearch.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<p align="center">
  <img src="https://raw.githubusercontent.com/bielu/bielu.bettersearch/main/assets/icon.png" alt="Bielu.BetterSearch logo" width="128">
</p>

A provider-agnostic search abstraction for .NET: describe a query once, then run it against whichever
search engine you configure. Elasticsearch, OpenSearch and an in-memory provider ship today, and a new engine is a new
provider package, with no change to the core.

📖 **Full documentation: [https://search.bielu.pl/](https://search.bielu.pl/)**

> **Status: beta, not production ready.** The public API will change before 1.0. The
> [roadmap](ROADMAP.md) is the plan for a stable release, and the [feature matrix](FEATURE_MATRIX.md)
> records what each provider really supports.

## Quick start

### 1. Install

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease

# Then one provider:
dotnet add package Bielu.BetterSearch.ElasticSearch --prerelease   # Elasticsearch
dotnet add package Bielu.BetterSearch.OpenSearch --prerelease      # OpenSearch
dotnet add package Bielu.BetterSearch.Lifti --prerelease           # in-memory
```

### 2. Register a provider

Elasticsearch, configured from `Bielu:BetterSearch:ElasticSearch`:

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;

builder.Services.AddBetterSearch(search => search.AddElasticSearch());
```

OpenSearch, configured from `Bielu:BetterSearch:OpenSearch`:

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddOpenSearch());
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
        }
      }
    }
  }
}
```

`AuthenticationType` is `NONE`, `PASSWORD` (basic, `Username` and `Password`) or `APIKEY` (an OpenSearch
3.x API key in `ApiKey`). An entry under `Indexes` overrides the connection string or the authentication
for that one index and takes the rest from the shared settings (set `"AuthenticationType": "NONE"` there to
turn the shared authentication off for that index), and `MaxResultWindow` (10,000 by default)
should match the indexes' `index.max_result_window`.

Or in memory:

```csharp
using Bielu.BetterSearch.DepedencyInjection;

builder.Services.AddBetterSearch(search => search.AddLiftiSearch());
```

### 3. Index and search

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
if (result.IsSuccess)
{
    foreach (var hit in result.Value.Items) { /* hit.Id, hit.Score, ... */ }
}
```

The [Getting Started](https://search.bielu.pl/articles/getting-started.html) guide covers this in more detail.

## Features

- ✅ **One query model** for every provider: term, terms, prefix, prefix phrase, fuzzy, date, numeric and
  string ranges, geo distance, raw Lucene, and nested boolean logic with Lucene semantics
- ✅ **Fluent query builder** in `Bielu.BetterSearch.FluentApi`
- ✅ **Paging, sorting, post filters and scores** on the Elasticsearch and OpenSearch providers
- ✅ **Errors as results**: searching and indexing return [FluentResults](https://github.com/altmann/FluentResults)
  `Result`s. Elasticsearch and OpenSearch fail a query that asks for facets or highlighting, but Lifti ignores paging,
  sorting, post filters, facets and highlighting, so check the [feature matrix](FEATURE_MATRIX.md) for what
  your provider applies
- ✅ **Shared provider tests**: provider-agnostic behavior tests in `BaseTests` run against every provider
- ✅ **.NET Aspire AppHost** that starts Elasticsearch and OpenSearch for local development and the integration tests

Facets and highlighting can be expressed but no provider runs them yet. See the
[feature matrix](FEATURE_MATRIX.md) for the full picture.

## Packages

| Package | Description |
| --- | --- |
| [`Bielu.BetterSearch.Abstractions`](https://www.nuget.org/packages/Bielu.BetterSearch.Abstractions/) | Query model (`SearchQuery` and its sub-queries) and service interfaces |
| [`Bielu.BetterSearch.Models`](https://www.nuget.org/packages/Bielu.BetterSearch.Models/) | `SearchDocument` and the request models |
| [`Bielu.BetterSearch.Core`](https://www.nuget.org/packages/Bielu.BetterSearch.Core/) | `AddBetterSearch` and the indexing and search services |
| [`Bielu.BetterSearch.FluentApi`](https://www.nuget.org/packages/Bielu.BetterSearch.FluentApi/) | Fluent query builder |

### Providers

| Package | Engine | Status |
| --- | --- | --- |
| [`Bielu.BetterSearch.ElasticSearch`](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/) | [Elasticsearch](https://www.elastic.co/elasticsearch) | Available |
| [`Bielu.BetterSearch.OpenSearch`](https://www.nuget.org/packages/Bielu.BetterSearch.OpenSearch/) | [OpenSearch](https://opensearch.org/) | Available |
| [`Bielu.BetterSearch.Lifti`](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) | In memory, on [LIFTI](https://github.com/mikegoatly/lifti) | Available; to be replaced by Lucene.NET |
| `Bielu.BetterSearch.Lucene` | [Lucene.NET](https://lucenenet.apache.org/), in memory or on disk | Planned ([roadmap](ROADMAP.md)) |

## Documentation

- [Getting Started](https://search.bielu.pl/articles/getting-started.html)
- [Feature matrix](https://search.bielu.pl/articles/FEATURE_MATRIX.html)
- [Roadmap](https://search.bielu.pl/articles/ROADMAP.html)
- [API reference](https://search.bielu.pl/api/index.html)

The [changelog](CHANGELOG.md) lists what changed in each release.

## Building from source

The repository needs the **.NET 10 SDK**. The solution is `src/Bielu.BetterSearch.slnx`. The Elasticsearch
and OpenSearch tests start their engine through the Aspire AppHost, so they need Docker.

```bash
dotnet build src/Bielu.BetterSearch.slnx
dotnet test --solution src/Bielu.BetterSearch.slnx
```

See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Every pull request needs a
[changeset](.changeset/README.md).

## License

Copyright (c) 2024 Arkadiusz Biel. Licensed under the [MIT License](LICENSE).
