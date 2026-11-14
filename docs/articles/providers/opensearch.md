# OpenSearch

[Bielu.BetterSearch.OpenSearch](https://www.nuget.org/packages/Bielu.BetterSearch.OpenSearch/) runs queries
on an [OpenSearch](https://opensearch.org/) cluster. It translates queries the same way as the
[Elasticsearch provider](elasticsearch.md), so the same queries give the same results, with paging, sorting,
post filters, facets, highlighting and scores.

> [!WARNING]
> Invalid or missing connection settings make searching and every indexing method return a failed `Result`
> whose message names the setting to fix, such as `Bielu:BetterSearch:OpenSearch:SharedIndexSettings:ConnectionString`.
> A request that throws, as it does when `ThrowExceptions()` is on in the connection settings, is a failed
> `Result` from every indexing method, with the exception as its cause. So is a client factory that throws.
> `OperationCanceledException` still propagates.
> A request cancelled through the `CancellationToken`, before or during the request, throws
> `OperationCanceledException` from every indexing method. A write cancelled after the server received it may
> still have been applied.

## Install

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease
dotnet add package Bielu.BetterSearch.OpenSearch --prerelease
```

## Register

`AddOpenSearch()` reads its settings from `Bielu:BetterSearch:OpenSearch`, so pass the configuration in. The
`AddBetterSearch` overload without configuration leaves the provider with empty settings.

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.OpenSearch.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddOpenSearch());
```

## Configure

```json
{
  "Bielu": {
    "BetterSearch": {
      "OpenSearch": {
        "SharedIndexSettings": {
          "ConnectionString": "http://localhost:9200",
          "AuthenticationType": "PASSWORD",
          "AuthenticationDetails": { "Username": "admin", "Password": "..." }
        },
        "Indexes": {
          "people": { "ConnectionString": "http://people-cluster:9200" }
        },
        "MaxResultWindow": 10000
      }
    }
  }
}
```

| Setting | Meaning |
|---------|---------|
| `SharedIndexSettings` | Connection used for every index without its own entry |
| `ConnectionString` | Cluster URL, `http` or `https`; a missing or invalid one makes each call for that index return a failed `Result` |
| `AuthenticationType` | `NONE`, `PASSWORD` (basic, `Username` and `Password`) or `APIKEY` (an OpenSearch 3.x API key in `ApiKey`, sent as `Authorization: ApiKey <key>`) |
| `Indexes` | Per-index settings; an entry can set its own connection string or authentication, and takes whatever it leaves out from `SharedIndexSettings`. An entry's `"AuthenticationType": "NONE"` turns the shared authentication off for that index. Indexes that end up with the same connection and authentication share one client, and changed settings take effect on the next call. Keys match index names whatever their case, so `People` applies to the index `people` |
| `Mappings` | Explicit field mappings, in `SharedIndexSettings` or an `Indexes` entry; see [Field mappings](#field-mappings) |
| `BulkBatchSize` | Most documents `IndexMultipleDocumentsAsync` sends in one bulk request, 1000 by default, in `SharedIndexSettings` or an `Indexes` entry; see [Indexing several documents](#indexing-several-documents). It must be greater than 0, or startup validation fails |
| `WriteVisibility` | `Default` or `WaitUntilSearchable`, in `SharedIndexSettings` or an `Indexes` entry; see [When writes are searchable](#when-writes-are-searchable). An entry that leaves it out takes the shared value, and `"WriteVisibility": "Default"` there turns the shared one off |
| `RequestTimeout` | How long a request may take, such as `"00:00:10"`, in `SharedIndexSettings` or an `Indexes` entry; unset, the client's default of 60 seconds. The client also limits a request and its retries together to this time, unless `configureClient` sets `MaxRetryTimeout`. It must be greater than 0 and at most `24.20:31:23.647` (`int.MaxValue` milliseconds, the longest the client accepts), or startup validation fails |
| `MaxRetries` | How many times the client retries a failed request, in `SharedIndexSettings` or an `Indexes` entry; unset, the client's default. It must not be negative, or startup validation fails |
| `IndexPrefix` | Put before every index name sent to the server, so applications or environments can share a cluster: with `app-`, the index `people` is `app-people` on the server. Indexing, search, `GetDocumentAsync`, `CountAsync`, `UpdateDocumentAsync`, removal, the index checks, `DeleteIndexAsync` and rebuilds all add it, and you keep using `people`. Lower case only, without `\ / * ? " < > \| , # :` or spaces, and not starting with `-`, `_` or `+`; startup validation fails otherwise, naming `IndexPrefix` |
| `MaxResultWindow` | Deepest result a search may page to, 10,000 by default; match it to the indexes' `index.max_result_window`. It must be greater than 0, or startup validation fails |

You can also set the options in code with `services.Configure<OpenSearchOptions>(...)`.

The options are validated at startup (`ValidateOnStart`), so a host with invalid settings fails to start with
`OptionsValidationException`, listing every setting that is wrong, each by its configuration path. The connection
string and authentication are checked as each call checks them, after merging each `Indexes` entry with
`SharedIndexSettings`. For example `ConnectionString is not an http or https URL. Set
Bielu:BetterSearch:OpenSearch:Indexes:people:ConnectionString.` A failure for an `Indexes` entry names only the setting that changes its value: the entry's own path when it sets the value, and the `SharedIndexSettings` path when it inherits it, as an entry takes the authentication details only with its own `AuthenticationType`.
Connection messages never include the connection string or a credential; the other messages show the rejected value, such as `MaxResultWindow is 0`. A missing section, or one whose name is misspelled, gives the single
failure `No Bielu:BetterSearch:OpenSearch settings were found.`, naming the section. The shared settings are not checked on their own when they set
no connection string and every `Indexes` entry sets its own connection: shared authentication is then checked with each index that inherits it, and an index not listed fails when it is used. `MaxResultWindow` must be greater than 0. The same
checks run whenever the options are read, so after a configuration reload to invalid settings, every call returns a
failed `Result` with the same messages.

### Client settings

For anything the options don't cover, such as a proxy, a header or certificate validation, pass `configureClient`. It
gets the client's `ConnectionSettings` after the provider has applied the options, so it can override them too:

```csharp
builder.Services.AddBetterSearch(builder.Configuration, search => search.AddOpenSearch(
    configureClient: settings => settings.Proxy(new Uri("http://proxy:3128"), "user", "password")));
```

The provider keeps one client per distinct connection: the connection string, the authentication, `RequestTimeout`
and `MaxRetries`, so two indexes with different timeouts get two clients. The callback runs once for each client, when
it is created, not on every request; two calls racing to create the same client can each run it, and only one of the
clients is kept. It runs for every client the provider creates, whichever `Indexes` entry the client is for.

## How queries behave

Documents are stored with their values under `fields`, as with Elasticsearch, so a query on `Name` searches
`fields.Name`.

- **Text fields are analyzed.** `TermQuery` and `PrefixQuery` match analyzed tokens and ignore ASCII case only.
  Use `Field.keyword` to match the whole value.
- **`PrefixQuery` needs a text or keyword field, and `PrefixPhraseQuery` a text field.** On a field holding
  numbers or dates, OpenSearch rejects the query and the search returns a failed `Result`.
- **`StringRange` bounds are case-sensitive.** They are compared to the indexed tokens as given, so on an
  analyzed field, whose tokens are lower-cased, `"a"`–`"z"` matches `Jane` but `"A"`–`"Z"` doesn't. Pass
  lower-case bounds, or use `Field.keyword` to compare whole values.
- **Sorting on a text field** is rejected by OpenSearch. A field mapped as `Text` in `Mappings` sorts on its `keyword`
  sub-field under its plain name; an unmapped text field still sorts as `Field.keyword`.
- **`SpatialSearchQuery`** needs the field mapped as `GeoPoint` in `Mappings`; dynamic mapping doesn't create one.
- **`LuceneQuery`** field names need the `fields.` prefix.
- **`FuzzyQuery`** allows at most 2 edits, and uses `AUTO` when `Fuziness` is unset. A value that is not a string or
  a number, such as a date or a boolean, returns a failed `Result`.
- **Dates are stored and queried as ISO 8601 text in UTC**, with fractional seconds only when they are not zero
  (`2026-11-08T13:05:00Z`, `2026-11-08T13:05:00.123Z`). A local `DateTime` and a `DateTimeOffset` are converted to
  UTC, and a `DateTime` with `Kind.Unspecified` is taken as UTC, so a date comes back from a search in UTC. A
  `TermQuery`, `TermsQuery` or `PrefixQuery` with a date value matches that text on a keyword field. Dynamic mapping maps these strings as
  dates, which OpenSearch rejects a prefix query on. Re-index documents indexed before this version, which keep the text
  the client wrote then.
- **Total hit count** is exact up to `MaxResultWindow` matches, then a lower bound.

## Field mappings

Indexes are created with dynamic mapping, which makes a string a `text` field with a `keyword` sub-field,
unless the first value it sees looks like a date (`2026-11-13`), which date detection maps as `date` with no sub-field.
`Mappings` maps fields explicitly instead. Each entry names a `Field`, as queries name it (`City`, not
`fields.City`), and a `Type`: `Text`, `Keyword`, `Date`, `Long`, `Integer`, `Short`, `Byte`, `Double`, `Float`,
`Boolean` or `GeoPoint`. A `Text` field can set an `Analyzer` and a `Date` field a `Format`.

```json
"SharedIndexSettings": {
  "Mappings": [
    { "Field": "City", "Type": "Keyword" },
    { "Field": "Location", "Type": "GeoPoint" }
  ]
},
"Indexes": {
  "articles": {
    "Mappings": [ { "Field": "Title", "Type": "Text", "Analyzer": "english" } ]
  }
}
```

- An `Indexes` entry's mappings add to the shared ones, and replace a shared mapping of the same field.
- Fields not listed keep dynamic mapping.
- **`Keyword`** holds the whole value. `TermQuery` matches it whole, ignoring case, and sorting uses it, both
  under the plain field name.
- **`Text`** is analyzed and gets a `keyword` sub-field (`ignore_above` 256), as dynamic mapping gives it.
  `TermQuery` and `TermsQuery` match one token the field's analyzer produced, ignoring case: with the default
  analyzer that is one word, as on every provider, while with another `Analyzer` the term must equal one of its
  tokens (with `keyword`, the whole value). Sorting uses `Field.keyword`
  automatically, so callers sort on `Field`. That sort compares whole values as given: upper case sorts
  before lower case (`Banana` before `apple`), a multi-valued field sorts ascending by its lowest value, and a
  value longer than 256 characters isn't in the sub-field, so it sorts as if the field were missing.
- **`GeoPoint`** makes `SpatialSearchQuery` work. A point is a `"lat,lon"` string or a dictionary with `lat`
  and `lon`.
- **`Date`** with a `Format` must accept the values documents hold, or indexing them fails. The provider writes a
  `DateTime` or `DateTimeOffset` as ISO 8601 text in UTC (`2026-11-08T13:05:00Z`) and a `DateOnly` as `yyyy-MM-dd`, so a custom format has to
  accept ISO 8601 as well, for example `strict_date_optional_time||yyyy-MM-dd`. `yyyy-MM-dd` alone rejects every
  `DateTime` document.

`CreateIndexAsync` creates the index with its mappings. It returns `false` when the index already exists,
leaving its mappings as they are, and a failed `Result` for any other rejection, such as an unknown analyzer.
`EnsureIndexExistsAsync` creates a missing index the same way. When the index exists, or another caller created
it in between, it adds the mappings to it. It returns `true` whenever the index exists afterwards. A mapping that conflicts with how the field is already mapped, such as
`Keyword` on a field dynamic mapping already made `text`, returns a failed `Result` naming the field. Re-index
into a new index to change a field's type. When the cluster takes the mappings but doesn't acknowledge them in
time, the result is failed too: the mappings aren't rolled back, and calling `EnsureIndexExistsAsync` again
confirms them.

The options are validated at startup (`ValidateOnStart`) and whenever they are read. A missing `Field` or
`Type`, a `Field` starting with `fields.`, the same field twice in one list, an `Analyzer` on a field that isn't
`Text` or a `Format` on a field that isn't `Date` throws `OptionsValidationException`, naming the setting, such as
`Bielu:BetterSearch:OpenSearch:Indexes:articles:Mappings:0:Type`.

## Highlighting

Each entry in `HighlightQueries` asks for the fragments of one field (`Field`) or of every field
(`IsAllField`, sent as `fields.*`). The fragments come back on each hit, in `ISearchHit.Highlights`, keyed by
the field name without the `fields.` prefix (`Name`, also when the request names `fields.Name`), with each matched term between `<em>` and `</em>`
(`SearchHit.HighlightPreTag` and `HighlightPostTag`).

- **Terms the main query matched in that field** are marked. With `IsAllField`, only the fields the query
  matched have an entry. A request's own `Queries` replace the main query for that field, so they mark their
  own terms; they don't change which documents are hits.
- **A hit with nothing marked** has no entries, and a field with nothing marked has no entry.
- **A multi-valued field** returns only the values that matched, each as its own fragment.
- **Fragments** use OpenSearch's default size and count.
- **Fragments are HTML-escaped.** The document text is escaped (`<` becomes `&lt;`, `&` becomes `&amp;`), so the
  `<em>` tags are the only markup and a fragment is safe to render as HTML.
- **Invalid requests** return a failed `Result`: a `null` entry, an entry with neither `Field` nor
  `IsAllField`, two entries for the same field, also under different names for it (`Name` and `fields.Name`),
  and a `null` clause in a request's own `Queries`.

## Facets

`FacetQueries` become OpenSearch aggregations, and their results come back in `ISearchResult.Aggregations`, read with
`RetrieveBucketAggregation(name)` and `RetrieveSingleAggregation(name)`:

- **`TermAggregation`** is a `terms` aggregation: one `KeyBucket` per value, the values with the most documents
  first, `Size` of them (10 when it is 0).
- **`MultiTermAggreation`** is a `multi_terms` aggregation: one `KeyBucket` per combination of values, each value
  in `Keys` and the joined key, such as `Red|acme`, in `Key`.
- **`FilterAggregationQuery`** is a `filter` aggregation: a `SingleAggregation` whose one bucket holds the number of
  documents its `Queries` match.
- **`NestedAggregations`** are sub-aggregations, counted per bucket and returned in the bucket's `Aggregations`.
- **Counts cover the main query's matches.** A post filter narrows the hits, not the counts.
- **A field mapped as `Text`** in `Mappings` is counted on its `keyword` sub-field, so facet on the plain name.
- **An unmapped text field**, such as a string field dynamic mapping created, can't be counted: the search returns
  a failed `Result` saying to map the field or to facet on `Field.keyword`, the sub-field dynamic mapping adds.
- **Keys are text**: a whole number comes back in the invariant culture, such as `5`.

## Indexing several documents

`IndexMultipleDocumentsAsync` sends each index's documents in bulk requests of at most `BulkBatchSize`
documents (1000 unless set), one after another, so a large import stays under the cluster's
`http.max_content_length` (100 MB unless changed), and returns how many documents it indexed. OpenSearch
applies each document of a bulk request on its own, so when it rejects some (a value that doesn't fit the field's
mapping, say), the remaining requests are still sent, and the `Result` fails naming their ids, with every id in the
error's `ids` metadata, and the other documents are already stored. When the first request is rejected as a
whole, such as a 403, the `Result` fails with OpenSearch's error and nothing after it is sent. When a later request
fails as a whole, the requests after it are not sent, and the `Result` fails naming every document not confirmed
as stored: the ones rejected so far, the ones in that request, which may or may not be stored, and the ones not
sent.

## When writes are searchable

By default a successful write is stored but not yet searchable: OpenSearch makes it visible at the next index
refresh, about a second later unless `index.refresh_interval` is changed. Set `WriteVisibility` to
`WaitUntilSearchable` to have every index and remove call return only once a search can see the write:

```json
"SharedIndexSettings": { "ConnectionString": "http://localhost:9200", "WriteVisibility": "WaitUntilSearchable" }
```

- `IndexDocumentAsync`, `IndexMultipleDocumentsAsync`, `RemoveDocumentAsync` and `RemoveDocumentsAsync` send
  `refresh=wait_for`, so the call waits for the next scheduled refresh rather than forcing one.
- `RemoveAllDocumentsAsync` sends `refresh=true`, because delete by query can't wait for a refresh; it refreshes
  the index once the documents are removed.
- **Cost**: each call can take up to the refresh interval, and `IndexMultipleDocumentsAsync` waits once for each
  bulk request of `BulkBatchSize` documents, so a large import waits many times. Leave it at `Default`
  for bulk loads and turn it on where a write must be visible straight away, such as tests and small sites.
- Waiting doesn't change what a failure means: a failed or cancelled call may still have stored the write.

Whatever `WriteVisibility` is, `RemoveAllDocumentsAsync` first refreshes the index, then sends the delete by query.
Delete by query removes only the documents that are searchable when it starts, so without the refresh a write that
was acknowledged under `Default` but not refreshed yet would be left behind. Remove all is rare, so the extra
request is a small cost. When the refresh fails, for example with a 403, a missing index or a failed shard, the call
returns a failed `Result` and removes nothing.

## Rebuilding an index without downtime

The indexing provider also implements `IIndexLifecycleProviderAsync`, which `IIndexLifecycleServiceAsync`, registered
by `AddBetterSearch` and by key like the other services, runs:

```csharp
var rebuild = await lifecycle.StartRebuildAsync("people");      // creates app-people-rebuild-<time>-<random>
if (rebuild.IsFailed)
{
    return;
}

var indexed = await lifecycle.IndexRebuildDocumentsAsync(rebuild.Value, documents); // searches still read the old index
if (indexed.IsFailed)
{
    await lifecycle.AbortRebuildAsync(rebuild.Value);           // deletes the new index; the old one stays live
    return;
}

var deleted = await lifecycle.CompleteRebuildAsync(rebuild.Value); // swaps the alias and deletes the old indexes
```

Complete doesn't know whether every document reached the new index, so check the result of each
`IndexRebuildDocumentsAsync` call and abort, rather than complete, when one fails.

- **Start** creates a new index named after the index (with the prefix), `-rebuild-`, the UTC start time and a random
  suffix, with the index's configured `Mappings`.
- **Index** writes the documents to the new index only. Each document's `Index` must be the index being rebuilt;
  the service checks the whole batch, as `IndexMultipleDocumentsAsync` does, before writing any of it.
- **Complete** refreshes the new index, and fails without touching the alias when the refresh fails on any shard. It
  then sends one update aliases request that points an alias named like the index at it and deletes, with
  `remove_index`, every index the alias pointed at before. OpenSearch applies the request atomically, so searches switch
  from the old documents to the new ones at once, and when OpenSearch rejects it, nothing changes and the alias stays on
  the old index. When OpenSearch doesn't acknowledge it in time, the `Result` fails but the change may still apply, so
  the old indexes may already be gone: call `CompleteRebuildAsync` again, which succeeds once the alias points at the
  new index. It returns the names of the deleted indexes. The first rebuild
  of an index created as a plain index replaces that index with the alias in the same request. Completing a rebuild
  that is already complete succeeds and deletes nothing.
- **Abort** deletes the new index and leaves the alias where it is. It fails, deleting nothing, once the rebuild is
  complete. Start gives the new index a pending alias, `<new index>-pending`. Complete removes it in its update
  aliases request, and Abort removes it before deleting the new index, each with `must_exist`, so when a complete and
  an abort of the same rebuild overlap, even in different processes, only one takes effect: either the rebuild
  completes and the abort fails, or the abort deletes the new index and the complete fails. Two overlapping completes
  both succeed.
- Each call reads `IndexPrefix` once, so a configuration reload during a call can't make it check one alias and change
  another. A rebuild whose new index is gone, aborted or deleted elsewhere, fails to complete and leaves the alias
  on the old index.
- A rebuild that names an index `StartRebuildAsync` could not have created for it fails without touching anything.

Writes to the index name while a rebuild runs go to the old index and are not copied to the new one, so pause them or
write them again after the swap. After a rebuild the index name is an alias: searches and writes go through it, and
`DeleteIndexAsync` deletes the index behind it, and so the alias.

## Removing documents

`DeleteDocumentRequest.Type` is the **index name**, not the document type held in `SearchDocument.Type`.

- **A document that is not there**: `RemoveDocumentAsync` succeeds, as the document is already gone.
- **An index that does not exist**: `RemoveDocumentAsync` returns a failed `Result`.
- **Several documents**: `RemoveDocumentsAsync(index, ids)` sends one bulk request and returns how many
  documents it removed; an id that is not there is not counted. When items fail, the `Result` fails naming
  their ids, and the other documents are already removed. A missing index returns a failed `Result` naming the ids.
  A request cancelled through the `CancellationToken`, before or during the request, throws
  `OperationCanceledException`.

## Getting, counting and updating documents

- **`GetDocumentAsync(index, id)`** reads the document with `GET index/_doc/id` and returns its fields as hits
  return them. A 404 that OpenSearch gave for a missing document (`found: false`) or a missing index
  (`index_not_found_exception`) is success with `null`. Any other failure, such as a 401, a 5xx or a 404
  without that answer (from a proxy, say), is a failed `Result`.
- **`CountAsync(query)`** sends `_count` with the query and its post filter, so the count is exact, with no
  cap at `MaxResultWindow`. A missing index is a failed `Result`, as a search there is.
- **`UpdateDocumentAsync(index, id, fields)`** sends a partial `_update` that replaces each named field under
  `fields` and keeps the others, and retries up to 3 times on a version conflict. It returns `false` for a
  missing document or index and creates neither. OpenSearch creates a missing index on an update, without the
  configured `Mappings`, so the provider checks the index exists first; an index deleted between that check and
  the update is still created again. `WriteVisibility` applies to it as to the other writes.

## Health check

As on Elasticsearch, `PingAsync` sends `HEAD /` with the client of the index it is given, or of
`SharedIndexSettings` when it is given none, and any answer other than 200, an unreachable server or invalid
settings is a failed `Result`. `AddBetterSearchHealthCheck()` from `Bielu.BetterSearch.HealthChecks` registers it as a health check,
which the application maps to an endpoint such as `/health`; see [observability](../feature-matrix.md#observability).

## Checking an index exists

`IndexExistsAsync` is success with `true` on a 200 and `false` on a 404. Any other answer, such as a 401, 403
or 5xx, or no answer at all, is a failed `Result`. Its message names the HTTP status when there was one, then
the server's error, or the transport's exception when the server sent none, and that exception is attached as
its cause. A request
cancelled through the `CancellationToken` throws `OperationCanceledException`. `EnsureIndexExistsAsync` returns
a failed check as it is rather than trying to create the index.

As on Elasticsearch, index operations report what happened: creating an existing index is success with
`false`, and any other rejection, such as a 403, is a failed `Result`. `EnsureIndexExistsAsync` and
`DeleteIndexAsync` succeed with `true`, and `EnsureIndexExistsAsync` is `true` too when another caller created
the index between its check and its create. The
[feature matrix](../feature-matrix.md) has the full detail.
