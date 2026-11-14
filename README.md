# Bielu.BetterSearch

[![CI](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/bielu.bettersearch/actions/workflows/buildAndPublishPackage.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Bielu.BetterSearch.Core.svg)](https://www.nuget.org/packages/Bielu.BetterSearch.Core/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bielu.BetterSearch.Core.svg)](https://www.nuget.org/packages/Bielu.BetterSearch.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<p align="center">
  <img src="https://raw.githubusercontent.com/bielu/bielu.bettersearch/main/assets/icon.png" alt="Bielu.BetterSearch logo" width="128">
</p>

A provider-agnostic search abstraction for .NET: describe a query once, then run it against whichever
search engine you configure. Elasticsearch, OpenSearch, Meilisearch and an in-memory provider ship today, and a new engine is a new
provider package, with no change to the core.

📖 **Full documentation: [https://search.bielu.pl/](https://search.bielu.pl/)**

> **Status: beta, not production ready.** The public API will change before 1.0. The
> [roadmap](ROADMAP.md) is the plan for a stable release, and the [feature matrix](docs/articles/feature-matrix.md)
> records what each provider really supports.

## Quick start

### 1. Install

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease

# Then one provider:
dotnet add package Bielu.BetterSearch.ElasticSearch --prerelease   # Elasticsearch
dotnet add package Bielu.BetterSearch.OpenSearch --prerelease      # OpenSearch
dotnet add package Bielu.BetterSearch.Meilisearch --prerelease     # Meilisearch
dotnet add package Bielu.BetterSearch.Lifti --prerelease           # in-memory
```

### 2. Register a provider

Elasticsearch, configured from `Bielu:BetterSearch:ElasticSearch`:

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddElasticSearch());
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

In every provider, keys under `Indexes` match index names whatever their case, so `Indexes:People` applies
to the index `people`.

Elasticsearch and OpenSearch also take explicit field mappings under `SharedIndexSettings:Mappings` or an
`Indexes` entry's `Mappings`, applied when the index is created: a `Keyword` field matches and sorts on its
whole value under its plain name, a `Text` field sorts on its `keyword` sub-field automatically, and a
`GeoPoint` field makes geo distance queries work. Fields not listed keep dynamic mapping, and invalid mappings
fail at startup. See [field mappings](docs/articles/providers/elasticsearch.md#field-mappings).

On Elasticsearch and OpenSearch a successful write becomes searchable at the next index refresh, about a
second later. Set `"WriteVisibility": "WaitUntilSearchable"` under `SharedIndexSettings` or an `Indexes` entry
to have index and remove calls return only once a search can see the write; Meilisearch and Lifti already
wait. See [when writes are searchable](docs/articles/providers/elasticsearch.md#when-writes-are-searchable).

Meilisearch, configured from `Bielu:BetterSearch:Meilisearch`:

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddMeilisearch());
```

```json
{
  "Bielu": {
    "BetterSearch": {
      "Meilisearch": {
        "SharedIndexSettings": {
          "ConnectionString": "http://localhost:7700",
          "ApiKey": "..."
        }
      }
    }
  }
}
```

`ApiKey` is the master key or an API key; leave it empty when the server has none. An entry under
`Indexes` without a `ConnectionString` uses the shared server and key. Meilisearch filters rather than
ranks, so fuzzy queries return a failed `Result`, and raw Lucene queries are translated into filters where
they can be: a term needs a field, and a Lucene fuzzy term matches the exact word with a `TranslationWarning`. Spatial queries work on one field, named by
`"GeoField"` under `Meilisearch`, holding a `"lat,lon"` string or a dictionary with `lat` and `lon`. Phrase
prefix queries store word runs for every text field; list the fields that need them in `"PhraseFields"` to keep
long texts small, and re-index after adding a field to it (see the [feature matrix](docs/articles/feature-matrix.md)).

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

var result = await search.SearchAsync(query, cancellationToken);
if (result.IsSuccess)
{
    foreach (var hit in result.Value.Items) { /* hit.Document.Id, hit.Score, ... */ }
}
```

To search one culture, index it in the plain `Culture` field and filter on it with the fluent API:

```csharp
Fields[CultureField.Name] = [CultureField.Value("en-US")];

var query = FluentApiSearchQueryCreator.Create()
    .WithSearchQuery(search => search.WithIndex("pages"))
    .CreateSearchQuery(search => search
        .CreateTermQuery(term => term.WithField("Title").WithValue("welcome"))
        .FilterByCulture("en-US"));
```

The [Getting Started](https://search.bielu.pl/articles/getting-started.html) guide covers this in more detail.

## Features

- ✅ **One query model** for every provider: term, terms, prefix, prefix phrase, fuzzy, date, numeric and
  string ranges, geo distance, raw Lucene, and nested boolean logic with Lucene semantics
- ✅ **Fluent query builder** in `Bielu.BetterSearch.FluentApi`
- ✅ **Paging, sorting, post filters, highlighting and scores** on the Elasticsearch and OpenSearch providers;
  paging, sorting and post filters on Meilisearch
- ✅ **Field mappings** on Elasticsearch and OpenSearch: `Text`, `Keyword`, `Date`, numeric, `Boolean` and
  `GeoPoint` fields, with an optional analyzer or format, validated at startup
- ✅ **Errors as results**: searching and indexing return [FluentResults](https://github.com/altmann/FluentResults)
  `Result`s. Every provider fails a query that asks for facets, except Lifti, which ignores them; Meilisearch and
  Lifti also fail a query that asks for highlighting. Lifti ignores paging, sorting and post filters, so check the [feature matrix](docs/articles/feature-matrix.md) for what
  your provider applies
- ✅ **Observability**: OpenTelemetry spans and metrics and `LoggerMessage` logs from the Core services, so every
  provider has them, wired with `AddBetterSearchInstrumentation()`, and a health check that pings the engine
  (`AddBetterSearchHealthCheck()`); see the [feature matrix](docs/articles/feature-matrix.md#observability)
- ✅ **Shared provider tests**: provider-agnostic behavior tests in `BaseTests` run against every provider
- ✅ **.NET Aspire AppHost** that starts Elasticsearch, OpenSearch and Meilisearch for local development and the
  integration tests, with [meilisearch-ui](https://github.com/riccox/meilisearch-ui) for browsing Meilisearch
  indexes and trying queries

Facets can be expressed but no provider runs them yet, and only Elasticsearch and OpenSearch highlight. See the
[feature matrix](docs/articles/feature-matrix.md) for the full picture.

## Packages

| Package | Description |
| --- | --- |
| [`Bielu.BetterSearch.Abstractions`](https://www.nuget.org/packages/Bielu.BetterSearch.Abstractions/) | Query model (`SearchQuery` and its sub-queries) and service interfaces |
| [`Bielu.BetterSearch.Models`](https://www.nuget.org/packages/Bielu.BetterSearch.Models/) | `SearchDocument` and the request models |
| [`Bielu.BetterSearch.Core`](https://www.nuget.org/packages/Bielu.BetterSearch.Core/) | `AddBetterSearch` and the indexing and search services |
| [`Bielu.BetterSearch.FluentApi`](https://www.nuget.org/packages/Bielu.BetterSearch.FluentApi/) | Fluent query builder |
| [`Bielu.BetterSearch.OpenTelemetry`](https://www.nuget.org/packages/Bielu.BetterSearch.OpenTelemetry/) | `AddBetterSearchInstrumentation()` for OpenTelemetry tracing and metrics |
| [`Bielu.BetterSearch.HealthChecks`](https://www.nuget.org/packages/Bielu.BetterSearch.HealthChecks/) | `AddBetterSearchHealthCheck()`, which pings the configured engine |

### Providers

| Package | Engine | Status |
| --- | --- | --- |
| [`Bielu.BetterSearch.ElasticSearch`](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/) | [Elasticsearch](https://www.elastic.co/elasticsearch) | Available |
| [`Bielu.BetterSearch.OpenSearch`](https://www.nuget.org/packages/Bielu.BetterSearch.OpenSearch/) | [OpenSearch](https://opensearch.org/) | Available |
| [`Bielu.BetterSearch.Meilisearch`](https://www.nuget.org/packages/Bielu.BetterSearch.Meilisearch/) | [Meilisearch](https://www.meilisearch.com/) | Available; filters only, see the feature matrix |
| [`Bielu.BetterSearch.Lifti`](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) | In memory, on [LIFTI](https://github.com/mikegoatly/lifti) | Available; to be replaced by Lucene.NET |
| `Bielu.BetterSearch.Lucene` | [Lucene.NET](https://lucenenet.apache.org/), in memory or on disk | Planned ([roadmap](ROADMAP.md)) |

## Documentation

- [Getting Started](https://search.bielu.pl/articles/getting-started.html)
- [Feature matrix](https://search.bielu.pl/articles/feature-matrix.html)
- [Roadmap](https://search.bielu.pl/articles/ROADMAP.html)
- [API reference](https://search.bielu.pl/api/index.html)

The [changelog](CHANGELOG.md) lists what changed in each release.

## Building from source

The repository needs the **.NET 10 SDK**. The solution is `src/Bielu.BetterSearch.slnx`. The Elasticsearch,
OpenSearch and Meilisearch tests start their engine through the Aspire AppHost, so they need Docker.

```bash
dotnet build src/Bielu.BetterSearch.slnx
dotnet test --solution src/Bielu.BetterSearch.slnx
```

To run the engines yourself, start `src/examples/Bielu.BetterSearch.Examples.AppHost` (`aspire run` from
`src/`, or `dotnet run` the project). For Meilisearch the Aspire dashboard links to Meilisearch's own search
preview and to meilisearch-ui; the master key is the `meilisearch-masterKey` parameter shown in the dashboard.

See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Every pull request needs a
[changeset](.changeset/README.md).

## License

Copyright (c) 2024 Arkadiusz Biel. Licensed under the [MIT License](LICENSE).
