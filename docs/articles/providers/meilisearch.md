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
> - **`LuceneQuery`** returns a failed `Result`: Meilisearch has no Lucene syntax.
> - **Relevance scores**: every hit has the same `Score`.
> - **Facets and highlighting** can be expressed, but a query that asks for them returns a failed `Result`.
> - **`SpatialSearchQuery`** works only on the field named by `GeoField`; without it, a failed `Result`.
> - **Remove all documents** reports a count of 0 through the service.
>
> A missing connection string makes each search and write for that index return a failed `Result`.

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
