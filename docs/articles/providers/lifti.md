# Lifti (in memory)

[Bielu.BetterSearch.Lifti](https://www.nuget.org/packages/Bielu.BetterSearch.Lifti/) keeps an in-memory index
built on [LIFTI](https://github.com/mikegoatly/lifti). It needs no server or configuration, which suits tests
and small data sets. It is due to be replaced by a Lucene.NET provider (see the
[roadmap](../../../ROADMAP.md)).

> [!WARNING]
> **What won't work**
>
> - **Silently ignored**: paging (`Page`, `PageSize`), sorting, post filters and facets. The query still
>   succeeds and returns every hit, so don't rely on them.
>   A `SortOrder` with a `null` entry or an entry with no `FieldName` still returns a failed `Result`, as on
>   the other providers.
> - **Highlighting** returns a failed `Result`: the index keeps only the words of each document, not its text,
>   so there is no text to mark the matched terms in.
> - **Range and spatial queries**: `DateRangeQuery`, `NumericRange`, `LongRange`, `StringRange` and
>   `SpatialSearchQuery` return a failed `Result`. A range with neither bound fails with the message every provider
>   gives for it, naming the missing bounds, rather than saying ranges are not supported, and so does a
>   `NumericRange` with a `NaN` or infinite bound.
> - **`LuceneQuery`** is not supported.
> - **Fields are ignored**: term, terms, fuzzy, prefix and phrase prefix queries match the value in any field.
> - **A value with no words**, such as `null`, `""` or `-`, returns a failed `Result` from a term, fuzzy, prefix or
>   phrase prefix query, as it could never match. A terms query fails when one of its values has no words; a terms
>   query whose list is `null` or empty succeeds with no hits.
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

A write is searchable as soon as the call returns, as the index is in memory.

## How queries behave

- **`MUST`, `SHOULD` and `MUSTNOT`** follow Lucene semantics, as on the other providers: alongside `MUST`,
  `SHOULD` changes nothing, and a nested bool with only `MUSTNOT` clauses excludes from its parent's matches.
- **A query that would match every document except some exclusions**, such as one with only `MUSTNOT`
  clauses, returns a failed `Result`, as Lifti has no query for every document.
- **`FILTER`** is treated as `MUST`, so it still scores.
- **Total hit count** is exact.
- **Values are split into words as the index splits them.** Field values are turned into text the same way on
  every machine and split into words at punctuation and spaces, with case and accents folded, so `3.5` is
  indexed as the words `3` and `5`. A term, terms, fuzzy, prefix or phrase prefix value goes through the same
  tokenizer, and its words must be next to each other, in order: `3.5` matches `3.5` but not `5.3` or `35`,
  a `DateTime` matches that date, as both are written as ISO 8601 text in UTC (`2026-11-08T00:00:00Z`), `żółw` matches `Żółw`, and `jane d` as a phrase prefix matches
  `Jane Doe`. Fuzzy matches each word on its own, and prefix and phrase prefix match the last word as a prefix.
  A prefix value that ends with punctuation or a space, such as `3.`, has a complete last word, so that word
  matches whole and must be followed by another word: `3.` matches `3.5`, not `35` or `3`. As Lifti has no
  wildcard that excludes one word, that following word can be the next value of a multi-valued field, so `3.`
  also matches a field holding `3` and `x`.
  One word of a value, such as the year `2026` of a date, still matches the whole value, as a word does in
  any text field.
- **Multi-valued fields**: the words of one value are never next to the words of another, so `3.5` does not
  match a field holding `3` and `5`.

## Indexing several documents

`IndexMultipleDocumentsAsync` adds each index's documents with one `AddRangeAsync`, which Lifti applies as one
change, and returns how many documents it indexed. When Lifti rejects one document (one without an id, say),
the `Result` fails naming every id of that index, with all of them in the error's `ids` metadata, and none of
them is stored.

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

## Index prefix and rebuilds

Lifti has no `IndexPrefix`, as its indexes live in the process and nothing else shares them. It has no index rebuilds
either: the provider does not implement `IIndexLifecycleProviderAsync`, so every `IIndexLifecycleServiceAsync` call
returns a failed `Result`.

## Errors

When a custom `ILiftiIndexManager` or `IClientFactoryAsync<IFullTextIndex<string>>` throws, every indexing method
returns a failed `Result` with the exception as its cause. An `OperationCanceledException` from either of them
propagates. A failed `Result` from `ILiftiIndexManager.CreateIndexAsync` is returned by `CreateIndexAsync` with
all of its errors.

The [feature matrix](../feature-matrix.md) has the full detail.
