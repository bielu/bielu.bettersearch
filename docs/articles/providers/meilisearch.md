# Meilisearch

[Bielu.BetterSearch.Meilisearch](https://www.nuget.org/packages/Bielu.BetterSearch.Meilisearch/) runs
queries on a [Meilisearch](https://www.meilisearch.com/) server. Every query runs as a Meilisearch filter,
which matches or doesn't, so hits aren't ranked. Paging, sorting and post filters work.

> [!WARNING]
> **What won't work**
>
> - **`FuzzyQuery`** returns a failed `Result`: filters only match exact values, and Meilisearch's typo
>   tolerance applies to the whole search text, not one field.
> - **`PrefixQuery` and `PrefixPhraseQuery`** with a value that isn't a string, such as a number, return a
>   failed `Result`.
> - **`LuceneQuery`** is parsed with Lucene.NET's `QueryParser` and translated into a filter, but only part of
>   the syntax translates. A term without a field (`jane` rather than `Name:jane`), proximity, boosts, regular
>   expressions and wildcards other than a trailing `*` return a failed `Result`. Fuzzy terms and quoted phrases of
>   several words translate only approximately, and the result carries a `TranslationWarning`; a quoted single word
>   is an exact term. See [Lucene queries](#lucene-queries).
> - **Relevance scores**: every hit has the same `Score`.
> - **Facets** can be expressed, but a query that asks for them returns a failed `Result`.
> - **Highlighting** returns a failed `Result`. Meilisearch marks only matches of the search text, and this
>   provider runs every query as a filter with no search text, so there is nothing to mark.
> - **`SpatialSearchQuery`** works only on the field named by `GeoField`; without it, a failed `Result`.
>
> A missing connection string makes each search and write for that index return a failed `Result`. An
> `OperationCanceledException` thrown by the client factory propagates rather than becoming a failed `Result`.

## Install

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease
dotnet add package Bielu.BetterSearch.Meilisearch --prerelease
```

## Register

`AddMeilisearch()` reads its settings from `Bielu:BetterSearch:Meilisearch`, so pass the configuration in.
The `AddBetterSearch` overload without configuration leaves the provider with empty settings.

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Meilisearch.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddMeilisearch());
```

## Configure

```json
{
  "Bielu": {
    "BetterSearch": {
      "Meilisearch": {
        "SharedIndexSettings": {
          "ConnectionString": "http://localhost:7700",
          "ApiKey": "..."
        },
        "GeoField": "Location",
        "PhraseFields": [ "Name" ]
      }
    }
  }
}
```

| Setting | Meaning |
|---------|---------|
| `SharedIndexSettings` | Server used for every index without its own connection string |
| `ConnectionString` | Server URL, such as `http://localhost:7700` |
| `ApiKey` | The master key or an API key; leave it empty when the server has none |
| `Indexes` | Per-index settings. An entry with its own `ConnectionString` uses only its own key; an entry without one uses the shared server, with its own `ApiKey` if set or the shared one. Keys match index names whatever their case, so `People` applies to the index `people` |
| `TaskTimeout` | How long to wait for a write task to finish, 30 seconds by default. A timeout fails the `Result` but doesn't cancel the task, so the write may still be applied; the error's `taskUid` metadata holds the task's uid |
| `MaxTotalHits` | Deepest result a search may page to, 10,000 by default; the provider sets it as each index's `pagination.maxTotalHits` |
| `SettingsCheckInterval` | How long a write trusts an index's settings before checking them again, 5 seconds by default |
| `GeoField` | The field holding each document's location, a `"lat,lon"` string or a dictionary with `lat` and `lon`. The provider copies it to `_geo`, and indexing a document whose point isn't valid returns a failed `Result` |
| `PhraseFields` | The fields phrase prefix queries can search. Unset, every text field stores the word runs they need; set, a phrase prefix on any other field returns a failed `Result` |

You can also set the options in code with `services.Configure<MeilisearchOptions>(...)`.

## How documents are stored

Filters compare whole values, so alongside each document the provider stores:

- each text field's words, lower-cased (`tokens`), split on anything that isn't a letter or digit;
- the runs of up to 16 words a phrase prefix can start at (`phrases`), for every field or only `PhraseFields`;
- each date as Unix milliseconds (`dates`).

Under `fields`, a date is stored as ISO 8601 text in UTC (`2026-11-08T13:05:00Z`), as Elasticsearch and OpenSearch
store it, so it comes back from a search that way.

A query on `Name` filters those copies; a query on `fields.Name` compares the value as given, as `Name.keyword`
does on Elasticsearch. **Re-index** documents indexed before this version, and after adding a field to
`PhraseFields`: until then they don't match.

Writes wait for Meilisearch's task, so a successful `Result` means the write was applied. They also add the
filterable and sortable attributes the provider needs to the index settings, keeping any added by others.

## How queries behave

- **`TermQuery`** on a string matches one word, ignoring case, including non-ASCII (`Żółw` matches `żółw`). On
  `fields.Name` it matches the whole value, also ignoring case. A string that reads as a number or boolean
  also matches that value.
- **`PrefixPhraseQuery`** matches at most 16 words.
- **Numbers** in a filter are written in the invariant culture (`3.5`, never `3,5`), and a positive exponent drops
  its `+` (`1E20`, not `1E+20`), as Meilisearch rejects `1E+20` in a filter. A `TermSubQuery` or `TermsSubQuery`
  value of `NaN` or an infinity returns a failed `Result`, as a Meilisearch filter reads `NaN` and `Infinity` as text
  and would match the strings `"NaN"` and `"Infinity"` rather than a number.
- **`LongRange`** values past 2^53 lose precision, as Meilisearch compares numbers as doubles.
- **Dates** without a time zone are read as UTC.
- **Field names** can only contain letters, digits, `_`, `-` and `.`, and **document ids** letters, digits, `-`
  and `_`; anything else returns a failed `Result`.
- **Total hit count** is exact up to `MaxTotalHits` matches, then that number.

## Lucene queries

A `LuceneSubQuery` is parsed with Lucene.NET's classic `QueryParser`, the syntax Elasticsearch's `query_string`
accepts, and translated into one filter. `OR` is the default operator, as on Elasticsearch.

| Lucene | Filter | Outcome |
|---|---|---|
| `Name:jane`, `fields.Name:jane` | a word of `Name`, as `TermQuery`; a value that reads as a number or boolean also matches that value | exact |
| `Name:jo*` | a word of `Name` starting with `jo`, as `PrefixQuery` | exact |
| `Age:[30 TO 40]`, `Age:{30 TO 40}`, `Age:[30 TO *]` | the words of `Age` in the range; bounds that read as numbers also compare numbers, and ISO 8601 dates compare dates; a date without a time rounds up to the end of the day for an included maximum or an excluded minimum, as Elasticsearch does | exact |
| `_exists_:City`, `City:*`, `City:[* TO *]` | `fields.City EXISTS` | exact |
| `AND`, `OR`, `NOT`, `+`, `-`, `( )` | `AND`, `OR`, `NOT`, with the [boolean semantics](../feature-matrix.md#boolean-semantics) of `BoolQuery` | exact |
| `Name:"jane"` | the word `jane`, as `Name:jane` | exact |
| `Name:"jane doe"`, `Name:"jane-doe"` | a run of words starting with `jane doe` while `Name` also holds the word `doe`; Meilisearch trims a trailing space from a filter value, so it cannot end the run after `doe` | ⚠️ `TranslationWarning` |
| `Name:jane~`, `Name:jane~1` | the exact word: filters have no edit distance | ⚠️ `TranslationWarning` |
| `jane` (no field), `Name:foo-bar` (one unquoted term that splits into several words; `Name:(jane doe)` is two terms and translates), `Name:""`, `"a b"~2`, `^2`, `/regex/`, `j?ne`, `*ane*`, parentheses nested more than 32 deep, a phrase of several words longer than 16 words or on a field outside `PhraseFields` | | ❌ failed `Result` naming the clause |

A warning is a FluentResults success reason, so a caller finds it with
`result.Successes.OfType<TranslationWarning>()`. `SearchServiceAsync` returns it with the hits.

## Indexing several documents

`IndexMultipleDocumentsAsync` adds each index's documents in one task and returns how many documents it
indexed. Meilisearch applies the task or fails it as a whole, so when it rejects one document (an invalid id,
say), the `Result` fails naming every id of that index, with all of them in the error's `ids` metadata, and
none of them is stored. When the wait for the task times out (`TaskTimeout`), the `Result` fails saying the
task was queued and may still index any or all of the documents, with the task's uid in the error's `taskUid`
metadata. When the request adding the documents fails with no answer from Meilisearch (a dropped connection,
say), the `Result` fails saying Meilisearch may still index any or all of them, with no `taskUid`. A failure
before the documents are sent, such as a timeout waiting for the index's settings update, stores none of them
and says `0 of N documents were indexed`, with no `taskUid`.

## Removing documents

`DeleteDocumentRequest.Type` is the **index name**, not the document type held in `SearchDocument.Type`.

- **A document that is not there**: `RemoveDocumentAsync` succeeds, as the document is already gone.
- **An index that does not exist**: `RemoveDocumentAsync` returns a failed `Result` with Meilisearch's error.
- **Several documents**: `RemoveDocumentsAsync(index, ids)` sends one deletion task and returns how many
  documents it removed. Meilisearch applies the task or fails it as a whole, so a failure names every id. A
  missing index returns a failed `Result` naming the ids, with Meilisearch's error. When the wait for the task
  times out (`TaskTimeout`), the `Result` fails saying the task was queued and may still remove any or all of the
  ids, with the task's uid in the error's `taskUid` metadata.

## Checking an index exists

`IndexExistsAsync` is success with `false` when Meilisearch answers `index_not_found`. Any other error, such as a
wrong API key or an unreachable server, is a failed `Result` caused by the client's exception, and a request
cancelled through the `CancellationToken` throws `OperationCanceledException`. `EnsureIndexExistsAsync` returns a
failed check as it is rather than trying to create the index.

The [feature matrix](../feature-matrix.md) has the full detail.
