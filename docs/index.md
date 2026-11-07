# Bielu.BetterSearch

A provider-agnostic search abstraction for .NET: describe a query once, then run it against whichever
search engine you configure.

> [!WARNING]
> **Beta, not production ready.** The public API will change before 1.0. The
> [feature matrix](../FEATURE_MATRIX.md) records what each provider really supports, and the
> [roadmap](../ROADMAP.md) is the plan for reaching a stable release.

<div class="landing-cards">

<div class="landing-card">

## One query model

Build a `SearchQuery` from term, prefix, fuzzy, range, geo-distance and nested boolean sub-queries, or
with the fluent API, and get the same results from every provider.

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

Searching and indexing are designed to return [FluentResults](https://github.com/altmann/FluentResults)
`Result`s, so anything a caller can act on is a failed result rather than an exception. Not every
unsupported option fails yet: Lifti ignores paging, sorting and post filters, as the
[feature matrix](../FEATURE_MATRIX.md) records. In this beta some paths still throw; the [roadmap](../ROADMAP.md) lists
known defects.

[API reference →](api/index.md)

</div>

</div>

## Providers

Each provider is its own NuGet package. The [feature matrix](../FEATURE_MATRIX.md) records exactly what
each one supports today.

<div class="landing-cards provider-cards">

<div class="landing-card">

### Elasticsearch <span class="provider-status available">Available</span>

[Bielu.BetterSearch.ElasticSearch](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/)
runs on an [Elasticsearch](https://www.elastic.co/elasticsearch) cluster. Check the feature matrix for
the query types and search features it supports.

```csharp
search.AddElasticSearch()
```

</div>

<div class="landing-card">

### Lifti <span class="provider-status available">Available</span>

[Bielu.BetterSearch.Lifti](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) is an in-memory
index built on [LIFTI](https://github.com/mikegoatly/lifti), for tests and small data sets. It is due to
be replaced by Lucene.NET.

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
