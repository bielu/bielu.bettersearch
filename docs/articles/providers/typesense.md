# Typesense

`Bielu.BetterSearch.Typesense` runs queries on a [Typesense](https://typesense.org/) server or cluster. Every
query runs as a Typesense filter (`filter_by`) with the search text `*`, which matches or doesn't, so hits
aren't ranked. Paging, sorting and post filters work.

The provider talks to Typesense's HTTP API itself: there is no official .NET client, and the API it uses
(collections, aliases, documents, import, export, delete by filter, `truncate` and `multi_search`) is small. It needs Typesense 28 or later
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
> - **`UpdateDocumentAsync`** reads the document, then sends a partial update. The JSON copy results come from,
>   `source`, is written whole, so a change another caller makes to other fields of the document in between is
>   lost from results, while it stays searchable.
> - **Index rebuilds** are not atomic, as Typesense has no request that moves an alias and deletes a collection
>   together: see [Rebuilding an index](#rebuilding-an-index). They have a single-writer contract: one rebuild of
>   an index at a time, with no two of its calls running at once. The first rebuild of an index created as a plain
>   collection deletes it before creating the alias, so searches in between fail.
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
| `IndexPrefix` | Put before every collection name sent to the server, so applications or environments can share a server: with `app-`, the index `people` is the collection `app-people`. Indexing, search, `GetDocumentAsync`, `CountAsync`, `UpdateDocumentAsync`, removal, the index checks, `DeleteIndexAsync` and rebuilds all add it, and you keep using `people`. Typesense accepts any characters in a collection name, so it is not checked |
| `RequestTimeout` | How long one request to one node may take, 30 seconds by default |
| `SchemaCheckInterval` | How long a search trusts the list of a collection's fields before reading it again, 5 seconds by default; a query on a field the list doesn't hold reads it again once the list is a second old, and a write through this provider makes the next search read it again |
| `GeoField` | The field holding each document's location, a `"lat,lon"` string or a dictionary with `lat` and `lon`. Indexing a document whose point isn't valid returns a failed `Result` |

You can also set the options in code with `services.Configure<TypesenseOptions>(...)`.

The options are validated at startup (`ValidateOnStart`), so a host with invalid settings fails to start with
`OptionsValidationException`, listing every setting that is wrong, each by its configuration path: the connection
string of the shared settings and of each `Indexes` entry after merging, and `SchemaCheckInterval`, which must not be
negative. A missing section gives the single failure `No Bielu:BetterSearch:Typesense settings were found.`, naming the
section. Each URL of `AdditionalNodes` is checked when a client is created. The same checks run whenever the options
are read, so after a configuration reload to invalid settings, a search returns a failed `Result` with the same
messages, and a write a failed `Result` that includes them.

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

## Getting, counting and updating documents

`GetDocumentAsync` reads the document and returns its fields from `source`, as hits return them; a missing
document or collection is success with `null`, and any other error, such as a 401, a failed `Result`. A document
whose id is `.`, `..`, `export` or `search`, which a document path can't address, is read by an export filtered on
its id.
`CountAsync` sends the filter `SearchAsync` would, post filter included, in a search for no hits (`per_page` 0)
and returns Typesense's `found`, which is exact. A missing collection is a failed `Result`, as a search there.

`UpdateDocumentAsync` reads the document, then sends a partial update, an import with `action=update`, holding
the named fields' new typed copies, `null` for every other copy a named field can have, and `source` with the
named fields replaced. So a field that changes kind, from an integer to a string say, no longer matches its old
value, even when another update gave it that value after the read, and the fields left out keep their stored values. Typesense fails the update of a missing
document or collection without creating either, which gives `false`, also when the document is removed between the
read and the update. `source` is written whole, so a change another caller makes to other fields in between is
lost from results, while its typed copies stay searchable. The collection's schema keeps every field a document
ever held, so after a field changes from an integer to a `double` in its only document, a sort on it still fails
as one on a field holding both.

## Rebuilding an index

The Typesense indexing provider implements `IIndexLifecycleProviderAsync`, so `IIndexLifecycleServiceAsync`
rebuilds an index through a [collection alias](https://typesense.org/docs/latest/api/collection-alias.html) named
like the index:

```csharp
var lifecycle = services.GetRequiredService<IIndexLifecycleServiceAsync>();
var rebuild = await lifecycle.StartRebuildAsync("people");      // creates app-people-rebuild-<time>-<random>
var indexed = await lifecycle.IndexRebuildDocumentsAsync(rebuild.Value, documents); // searches still read the old collection
var deleted = await lifecycle.CompleteRebuildAsync(rebuild.Value); // points the alias at it and deletes the old collection
```

- **Start** creates a new collection named after the index (with the prefix), `-rebuild-`, the UTC start time and
  a random suffix, with the provider's schema.
- **Fill** imports into the new collection as `IndexMultipleDocumentsAsync` does. A new collection that is gone,
  because the rebuild was aborted, fails the call and is not created again.
- **Complete** points the alias at the new collection, then deletes the collection it pointed at before, then
  deletes every other rebuild collection of the index, and returns the names it deleted. These are separate
  requests: when a delete fails, the alias already points at the new collection and the error names the collection
  left behind; calling `CompleteRebuildAsync` again deletes it. Completing a rebuild that is already complete
  succeeds and deletes, and returns, any rebuild collection of the index other than the live one, which cleans up
  after a complete that stopped after moving the alias, and after a rebuild that was never completed or aborted.
  Rebuild collections are found by name, `<prefix><index>-rebuild-<17 digits>-<8 hex digits>`, so another index whose
  name starts with the same text is not touched. A new collection that is gone fails to complete and leaves the
  alias where it was.
- **The first rebuild of a plain collection** has a short gap: Typesense refuses an alias named like a collection,
  so complete deletes that collection and then creates the alias. Searches and writes in that moment fail, and a
  write that recreates the collection is lost when it is deleted again. For zero downtime from the start, create the
  index through a rebuild (start, fill and complete it before anything else writes to the index name), or accept
  the gap once.
- **Abort** deletes the new collection and leaves the alias where it is; `false` when it is already gone. It fails,
  deleting nothing, when the alias points at it, as the rebuild is already complete.
- **One rebuild at a time.** Typesense has no atomic or conditional alias update, so rebuilds have a single-writer
  contract: at most one rebuild of an index is in progress at a time, and its start, fill, complete and abort don't
  run at the same time as another rebuild call for the same index, in this process or another. Running them at the
  same time is not supported and can leave an orphaned collection or the alias pointing at a deleted collection.
  Elasticsearch and OpenSearch have no such limit.
- After a rebuild the index name is an alias. Searches and writes go through it, `IndexExistsAsync` reads it as the
  collection behind it, and `DeleteIndexAsync` deletes that collection and the alias:
  deleting a collection by the alias's name would delete the collection and leave the alias pointing at nothing.
  A search reads the fields of the collection behind the alias again after a complete in this process, and within
  `SchemaCheckInterval` in other processes.

Writes to the index name while a rebuild runs go to the old collection and are not copied to the new one.

## Local development

The examples AppHost runs `typesense/typesense:29.0` with a generated API key (the `typesense-api-key`
parameter). The integration tests start it through the AppHost, so they need Docker.
