# Lifti (in memory)

[Bielu.BetterSearch.Lifti](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) keeps an in-memory index
built on [LIFTI](https://github.com/mikegoatly/lifti). It needs no server or configuration, which suits tests
and small data sets. It is due to be replaced by a Lucene.NET provider (see the
[roadmap](../../../ROADMAP.md)).

> [!WARNING]
> **What won't work**
>
> - **Silently ignored**: paging (`Page`, `PageSize`), sorting, post filters, facets and highlighting. The
>   query still succeeds and returns every hit, so don't rely on them.
> - **Range and spatial queries**: `DateRangeQuery`, `NumericRange`, `LongRange`, `StringRange` and
>   `SpatialSearchQuery` return a failed `Result`.
> - **`LuceneQuery`** is not supported.
> - **Fields are ignored**: term, terms, fuzzy, prefix and phrase prefix queries match the value in any field,
>   and accented terms don't match.
> - **Hits** only carry the document id, lower-cased, and `Score`; no stored fields.
> - **Persistence**: the index is lost on restart, and creating an index that exists replaces it, losing its
>   documents.
> - **Cancelling a search**: `SearchAsync` takes no `CancellationToken`.

## Install

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease
dotnet add package Bielu.BetterSearch.Lifti --prerelease
```

## Register

```csharp
using Bielu.BetterSearch.DepedencyInjection;

builder.Services.AddBetterSearch(search => search.AddLiftiSearch());
```

There is nothing to configure.

## How queries behave

- **`MUST`, `SHOULD` and `MUSTNOT`** follow Lucene semantics, as on the other providers: alongside `MUST`,
  `SHOULD` changes nothing, and a nested bool with only `MUSTNOT` clauses excludes from its parent's matches.
- **A query that would match every document except some exclusions**, such as one with only `MUSTNOT`
  clauses, returns a failed `Result`, as Lifti has no query for every document.
- **`FILTER`** is treated as `MUST`, so it still scores.
- **Total hit count** is exact.

The [feature matrix](../feature-matrix.md) has the full detail.
