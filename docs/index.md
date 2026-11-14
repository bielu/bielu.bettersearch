# Bielu.BetterSearch

A provider-agnostic search abstraction for .NET: describe a query once, then run it against whichever
search engine you configure.

> [!WARNING]
> **Beta, not production ready.** The public API will change before 1.0. The
> [feature matrix](articles/feature-matrix.md) records what each provider really supports, and the
> [roadmap](../ROADMAP.md) is the plan for reaching a stable release.

<div class="landing-cards">

<div class="landing-card">

## One query model

Build a `SearchQuery` from term, terms, prefix, phrase prefix, fuzzy, date, numeric and string range,
geo-distance, raw Lucene and nested boolean sub-queries, or with the fluent API. Boolean logic follows
Lucene semantics on every provider.

[Get started →](articles/getting-started.md)

</div>

<div class="landing-card">

## Pluggable providers

Pick the engine in one line of configuration. A new engine is a new provider package, with no change to
the core, and every provider runs the same shared tests.

[See the providers →](#providers)

</div>

<div class="landing-card">

## Errors as results

Searching and indexing return [FluentResults](https://github.com/altmann/FluentResults) `Result`s, so a
query a provider can't run is a failed result rather than an exception. Meilisearch fails a query that asks for
highlighting or for any facet other than a top-level term facet; Lifti fails one that asks for facets or
highlighting and ignores paging, sorting and post filters. In this beta some paths still throw; the [roadmap](../ROADMAP.md) lists known
defects.

[API reference →](api/index.md)

</div>

</div>

## Providers

Each provider is its own NuGet package. The [feature matrix](articles/feature-matrix.md) records exactly what
each one supports today.

<div class="landing-cards provider-cards">

<div class="landing-card">

### Elasticsearch <span class="provider-status available">Available</span>

[Bielu.BetterSearch.ElasticSearch](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/)
runs on an [Elasticsearch](https://www.elastic.co/elasticsearch) cluster, configured from
`Bielu:BetterSearch:ElasticSearch`, with paging, sorting, post filters and scores.

[Setup and limitations →](articles/providers/elasticsearch.md)

```csharp
search.AddElasticSearch()
```

</div>

<div class="landing-card">

### OpenSearch <span class="provider-status available">Available</span>

[Bielu.BetterSearch.OpenSearch](https://www.nuget.org/packages/Bielu.BetterSearch.OpenSearch/)
runs on an [OpenSearch](https://opensearch.org/) cluster, configured from `Bielu:BetterSearch:OpenSearch`
with basic or API key authentication. It translates queries the same way as the Elasticsearch provider.

[Setup and limitations →](articles/providers/opensearch.md)

```csharp
search.AddOpenSearch()
```

</div>

<div class="landing-card">

### Meilisearch <span class="provider-status available">Available</span>

[Bielu.BetterSearch.Meilisearch](https://www.nuget.org/packages/Bielu.BetterSearch.Meilisearch/)
runs on a [Meilisearch](https://www.meilisearch.com/) server, configured from
`Bielu:BetterSearch:Meilisearch`. Queries run as filters, so hits aren't ranked, and fuzzy and raw Lucene
queries return a failed `Result`.

[Setup and limitations →](articles/providers/meilisearch.md)

```csharp
search.AddMeilisearch()
```

</div>

<div class="landing-card">

### Lifti <span class="provider-status available">Available</span>

[Bielu.BetterSearch.Lifti](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) is an in-memory
index built on [LIFTI](https://github.com/mikegoatly/lifti), for tests and small data sets. It needs no
configuration and is due to be replaced by Lucene.NET.

[Setup and limitations →](articles/providers/lifti.md)

```csharp
search.AddLiftiSearch()
```

</div>

<div class="landing-card">

### Lucene.NET <span class="provider-status planned">Planned</span>

`Bielu.BetterSearch.Lucene` will run on [Lucene.NET](https://lucenenet.apache.org/), in memory or on
disk, with the same query semantics as Elasticsearch. See the [roadmap](../ROADMAP.md).

</div>

</div>
