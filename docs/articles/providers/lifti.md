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
> - **Phrase prefix** only works for one word: a value of several words, such as `jane d`, matches nothing.
> - **Decimals and dates** are turned into text the same way on every machine and split into words at
>   punctuation and spaces, so `3.5` is indexed as the words `3` and `5`. A term, terms or prefix query for a
>   whole decimal or date matches nothing, and a fuzzy one for a decimal can match a different number (`3.5`
>   is one edit from `35`); query one of its words instead, such as the day `08` of `11/08/2026 00:00:00`.
>   Integers are one word and match.
> - **Hits** only carry the document id and `Score`; no stored fields.
> - **Persistence**: the index is lost on restart, and creating an index that exists replaces it, losing its
>   documents.
> - **Missing index**: searching an index that does not exist creates it and returns no hits, where the other
>   providers return a failed `Result`.
> - **Removing from a missing index succeeds**: `RemoveDocumentAsync` on an index that does not exist returns
>   success, where the other providers return a failed `Result`. See [Removing documents](#removing-documents).
> - **Cancelling a running search**: the token is only checked before the search starts; once running, it
>   runs to the end.
> - **Culture filter** (`FilterByCulture`): the field is ignored, so a document holding the culture value in
>   another field matches too.

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

## Removing documents

`DeleteDocumentRequest.Type` is the **index name**, not the document type held in `SearchDocument.Type`.

- **A document that is not there**: `RemoveDocumentAsync` succeeds, as the document is already gone.
- **An index that does not exist**: `RemoveDocumentAsync` succeeds and does not create the index. Lifti keeps
  indexes in memory, so after a restart every index is missing until something writes to it; a missing index
  holds no documents, so the document is already gone. Reporting a failure there would make callers log errors
  or retry deletes that are already done. A misspelled index name therefore goes unnoticed, unlike on the other
  providers.
- **Several documents**: `RemoveDocumentsAsync(index, ids)` removes the ids one by one and returns how many
  were removed. A missing index succeeds with 0 and is not created, and cancellation,
  the index lookup, the client factory and the lock below work as with `RemoveDocumentAsync`.
- **Cancellation**: a cancelled token throws `OperationCanceledException`, as on Elasticsearch. Any other
  exception is caught and returned as a failed `Result` that keeps the exception as its cause. A failed lookup
  from `ILiftiIndexManager.GetIndexAsync` is returned with the manager's own errors, as supplied.
- **Which index**: the index manager (`ILiftiIndexManager.GetIndexAsync`) says whether the index exists; the
  document is then removed from the index the registered `IClientFactoryAsync<IFullTextIndex<string>>` returns,
  the same one indexing writes to. A custom `ILiftiIndexManager` must implement `GetIndexAsync` and return
  `null`, without creating the index, when it does not exist.
- **Removing while the index is deleted**: `RemoveDocumentAsync`, `RemoveDocumentsAsync`, `DeleteIndexAsync` and
  `RemoveAllDocumentsAsync` on the provider take a lock per index name, shared by every provider on the same
  `ILiftiIndexManager`. A delete therefore can't land between the lookup and the factory call, which would make
  the factory create the deleted index again. The lock does not cover calls made on the index manager directly.

The [feature matrix](../feature-matrix.md) has the full detail.
