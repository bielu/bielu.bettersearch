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

[Elasticsearch](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/) and an in-memory
[Lifti](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) provider today. A new engine is a new
provider package, with no change to the core.

[Feature matrix →](../FEATURE_MATRIX.md)

</div>

<div class="landing-card">

## Errors as results

Searching and indexing return [FluentResults](https://github.com/altmann/FluentResults) `Result`s, so
anything a caller can act on, including a feature an engine can't support, is a failed result rather than
an exception.

[API reference →](api/index.md)

</div>

</div>
