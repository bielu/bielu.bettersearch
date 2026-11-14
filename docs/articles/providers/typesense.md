# Typesense

`Bielu.BetterSearch.Typesense` runs queries on a [Typesense](https://typesense.org/) server or cluster. Every
query runs as a Typesense filter (`filter_by`) with the search text `*`, which matches or doesn't, so hits
aren't ranked. Paging, sorting and post filters work.

The provider talks to Typesense's HTTP API itself: there is no official .NET client, and the API it uses
(collections, import, delete by filter, `truncate` and `multi_search`) is small. It needs Typesense 28 or later
for `truncate`; the AppHost and the integration tests run 29.0.

> [!WARNING]
> **What won't work**
>
> - **`FuzzyQuery`** returns a failed `Result`: filters only match exact values, and Typesense's typo
>   tolerance applies to the search text.
> - **`LuceneQuery`** returns a failed `Result`: Typesense has no Lucene syntax, and the provider doesn't
>   translate it yet.
> - **`StringRange`** returns a failed `Result`: Typesense filters compare strings only for equality and prefixes.
> - **`MUSTNOT` on a range, a prefix or a `SpatialSearchQuery`** returns a failed `Result`. Typesense has no
>   `NOT` for an expression, so the provider pushes each negation down to its comparisons, and only an equality
>   (`TermQuery`, `TermsQuery`) has a negated form, `:!=`. A `MUSTNOT` term works, double negations included.
> - **`PrefixQuery` and `PrefixPhraseQuery`** with a value that isn't a string return a failed `Result`.
> - **Relevance scores**: every hit has the `Score` 1.
> - **Facets** and **highlighting** return a failed `Result`.
> - **`GetDocumentAsync`, `CountAsync` and `UpdateDocumentAsync`** are not implemented yet and return a failed `Result`.
> - **`SpatialSearchQuery`** works only on the field named by `GeoField`; without it, a failed `Result`.
> - **`PageSize`** above 250, the most Typesense returns in one page, returns a failed `Result`.
> - **`SortOrder`** takes at most 3 fields, as Typesense does, and only fields holding one number, date or
>   string in a document. A sort on a field that holds several values, or a boolean, in every document returns a
>   failed `Result`, as does a sort on a field holding an integer or date in some documents and a `double`,
>   `float` or `decimal` in others, which Typesense stores in separate fields.
>
> A missing connection string, or one that isn't an absolute `http` or `https` URL, makes each search and write
> for that index return a failed `Result` whose message names the setting to fix.

## Install

```bash
dotnet add package Bielu.BetterSearch.Typesense --prerelease
```

## Configure

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.Typesense.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddTypesense());
```

```json
{
  "Bielu": {
    "BetterSearch": {
      "Typesense": {
        "SharedIndexSettings": {
          "ConnectionString": "http://localhost:8108",
          "AdditionalNodes": [ "http://node-2:8108", "http://node-3:8108" ],
          "ApiKey": "..."
        },
        "GeoField": "Location"
      }
    }
  }
}
```

| Setting | Meaning |
|---------|---------|
| `SharedIndexSettings` | Server used for every index without its own connection string |
| `ConnectionString` | URL of a Typesense node, such as `http://localhost:8108` |
| `AdditionalNodes` | URLs of the cluster's other nodes. A request that can't reach a node, times out, or gets a 5xx answer is sent to the next one, in order, starting from `ConnectionString`. A delete moves on only when it can't connect: a node that timed out or answered 5xx may have applied it, so its timeout or answer is the result |
| `ApiKey` | The admin key, or a key allowed to manage the collections the provider uses |
| `Indexes` | Per-index settings. An entry with its own `ConnectionString` uses only its own nodes and key; an entry without one uses the shared server, with its own `AdditionalNodes` and `ApiKey` if set or the shared ones. Keys match index names whatever their case, so `People` applies to the index `people` |
| `RequestTimeout` | How long one request to one node may take, 30 seconds by default |
| `SchemaCheckInterval` | How long a search trusts the list of a collection's fields before reading it again, 5 seconds by default; a query on a field the list doesn't hold reads it again once the list is a second old, and a write through this provider makes the next search read it again |
| `GeoField` | The field holding each document's location, a `"lat,lon"` string or a dictionary with `lat` and `lon`. Indexing a document whose point isn't valid returns a failed `Result` |

You can also set the options in code with `services.Configure<TypesenseOptions>(...)`.

## How documents are stored

A Typesense collection has a schema, and an array field can't be sorted. The provider creates each collection
with one field pattern per kind of value, so a field's type never depends on the first document that holds it,
and stores, for a document field `Name`:

- `source`: every field as JSON text, not indexed, so a search returns exactly what was indexed (dates as ISO 8601
  text in UTC, a `3.0` double as `3.0`, a `long` above 2^53 unchanged);
- `t_Name`: the words of each string, lower-cased, split on anything that isn't a letter or digit;
- `s_Name`: each string whole;
- `p_Name`: the runs of up to 16 words a phrase prefix can start at;
- `i_Name`: each integer, as `int64`; `n_Name`: each other number, as Typesense's `float`;
- `d_Name`: each date as Unix milliseconds; `b_Name`: each boolean;
- `g_Name`: the point, for the field named by `GeoField`;
- `oi_Name`, `on_Name` or `os_Name`: the value of a field holding one integer or date, other number, or string,
  for sorting.

A field name can only contain letters, digits and `_`; indexing a document with any other name, or a `NaN` or
infinite number, returns a failed `Result`. A `ulong` above `long.MaxValue` is stored as a `float`.

Typesense applies each write before it answers, so a successful write is searchable when the call returns; there
is no `WriteVisibility` setting to turn on, as on Elasticsearch and OpenSearch. The first write to a missing index
creates its collection.

## How queries behave

- **`TermQuery`** on a string matches one word, ignoring case, including non-ASCII (`Żółw` matches `żółw`). On
  `fields.Name` it matches the whole value, also ignoring case, as Typesense compares strings. A string that reads
  as a number or boolean also matches that value. Numbers, booleans and dates match the value.
- **A field no document has** matches nothing, and its `MUSTNOT` matches every document. Typesense fails a filter
  on a field missing from the schema, so the provider reads the collection's fields and leaves such a comparison
  out of the filter.
- **Numbers** that aren't integers compare as Typesense `float`s, about seven significant digits: `0.3` matches
  `0.30000000000000004`, and `16777217` stored as a double matches `16777216`. Integers compare exactly as `int64`,
  so `LongRange` and a `long` term keep their precision past 2^53. A fractional range bound compares integers from
  the closest whole number inside the range, so at least 4.5 matches 5 and not 4.
- **`PrefixPhraseQuery`** matches at most 16 words.
- **`SpatialSearchQuery`** takes any Elasticsearch distance unit and sends the distance in kilometres.
- **A filter value** holding a backtick returns a failed `Result`, as a Typesense filter value can't contain one.
- **Total hit count** is exact.

## Indexing several documents

`IndexMultipleDocumentsAsync` checks every document first, so an invalid one stores none of them, then sends each
index's documents in import requests (`action=upsert`) of up to 1000 documents and 4 million characters of JSON,
one after another. Typesense applies each document on its own, so when it rejects one, the `Result` fails naming
its id, in the error's `ids` metadata too, and the others are already stored. If a request fails, the `Result`
says how many documents the earlier requests stored and names the rest; if it fails without an answer, Typesense
may have stored any or all of that request's documents. The shared test of a document the engine rejects is
skipped on Typesense: the provider converts every value to the type its schema declares, and Typesense accepted
every malformed document tried.

`RemoveDocumentsAsync` deletes by `id` filter, up to 100 ids and 4096 escaped characters of filter per request (an
id longer than that is sent alone), and returns how many were removed; if a
request fails, the error names the ids not yet sent or not confirmed, and the earlier requests' documents are
already removed. A missing index is a failed `Result`. `RemoveAllDocumentsAsync` truncates the collection and
returns the removed count.

## Local development

The examples AppHost runs `typesense/typesense:29.0` with a generated API key (the `typesense-api-key`
parameter). The integration tests start it through the AppHost, so they need Docker.
