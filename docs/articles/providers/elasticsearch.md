# Elasticsearch

[Bielu.BetterSearch.ElasticSearch](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/) runs
queries on an [Elasticsearch](https://www.elastic.co/elasticsearch) cluster, with paging, sorting, post
filters, highlighting and scores.

> [!WARNING]
> **What won't work**
>
> - **Facets** can be expressed, but a query that asks for them returns a failed `Result`.
> - **Bulk indexing**: Core's `IndexingServiceAsync.IndexMultipleDocumentsAsync` indexes one document at a time,
>   so this provider's `BulkAsync` path is never used.
>
> Invalid or missing connection settings throw from searching; every indexing method turns them into a
> failed `Result`. A request that throws, as it does when `ThrowExceptions()` is on in the client settings, is
> also a failed `Result` from every indexing method, with the exception as its cause.
> A request cancelled through the `CancellationToken`, before or during the request, throws
> `OperationCanceledException` from every indexing method. A write cancelled after the server received it may
> still have been applied.

## Install

```bash
dotnet add package Bielu.BetterSearch.Core --prerelease
dotnet add package Bielu.BetterSearch.ElasticSearch --prerelease
```

## Register

`AddElasticSearch()` reads its settings from `Bielu:BetterSearch:ElasticSearch`, so pass the configuration
in. The `AddBetterSearch` overload without configuration leaves the provider with empty settings.

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.ElasticSearch.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddElasticSearch());
```

## Configure

```json
{
  "Bielu": {
    "BetterSearch": {
      "ElasticSearch": {
        "SharedIndexSettings": {
          "ConnectionString": "http://localhost:9200",
          "AuthenticationType": "PASSWORD",
          "AuthenticationDetails": { "Username": "elastic", "Password": "..." }
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
| `SharedIndexSettings` | Connection used for every index without its own connection string |
| `ConnectionString` | Cluster URL |
| `AuthenticationType` | `NONE`, `PASSWORD` (basic, `Username` and `Password`), `CLOUD` (Elastic Cloud: the cloud id in `Id`, with `Username` and `Password`) or `CLOUDAPI` (the cloud id in `Id`, with an `ApiKey`) |
| `Indexes` | Per-index settings; an entry can set its own connection string or authentication, and takes whatever it leaves out from `SharedIndexSettings`. An entry's `"AuthenticationType": "NONE"` turns the shared authentication off for that index. Indexes that end up with the same connection and authentication share one client, and changed settings take effect on the next call. Keys match index names whatever their case, so `People` applies to the index `people` |
| `Mappings` | Explicit field mappings, in `SharedIndexSettings` or an `Indexes` entry; see [Field mappings](#field-mappings) |
| `WriteVisibility` | `Default` or `WaitUntilSearchable`, in `SharedIndexSettings` or an `Indexes` entry; see [When writes are searchable](#when-writes-are-searchable). An entry that leaves it out takes the shared value, and `"WriteVisibility": "Default"` there turns the shared one off |
| `MaxResultWindow` | Deepest result a search may page to, 10,000 by default; match it to the indexes' `index.max_result_window`. A page past it returns a failed `Result` |

You can also set the options in code with `services.Configure<ElasticSearchOptions>(...)`.

## How queries behave

Documents are stored with their values under `fields`, so a query on `Name` searches `fields.Name`.
Metadata fields such as `_id` and `_score` pass through unchanged.

- **Text fields are analyzed.** `TermQuery` and `PrefixQuery` match analyzed tokens and ignore ASCII case only
  (`Żółw` does not match `żółw`). Use `Field.keyword` to match the whole value.
- **`PrefixQuery` needs a text or keyword field, and `PrefixPhraseQuery` a text field.** On a field holding
  numbers or dates, Elasticsearch rejects the query and the search returns a failed `Result`.
- **`StringRange` bounds are case-sensitive.** They are compared to the indexed tokens as given, so on an
  analyzed field, whose tokens are lower-cased, `"a"`–`"z"` matches `Jane` but `"A"`–`"Z"` doesn't. Pass
  lower-case bounds, or use `Field.keyword` to compare whole values.
- **Sorting on a text field** is rejected by Elasticsearch. A field mapped as `Text` in `Mappings` sorts on its `keyword`
  sub-field under its plain name; an unmapped text field still sorts as `Field.keyword`.
- **`SpatialSearchQuery`** needs the field mapped as `GeoPoint` in `Mappings`; dynamic mapping doesn't create one.
- **`LuceneQuery`** field names need the `fields.` prefix.
- **`FuzzyQuery`** allows at most 2 edits, and uses `AUTO` when `Fuziness` is unset. A value that is not a string or
  a number, such as a date or a boolean, returns a failed `Result`.
- **Dates are stored and queried as ISO 8601 text in UTC**, with fractional seconds only when they are not zero
  (`2026-11-08T13:05:00Z`, `2026-11-08T13:05:00.123Z`). A local `DateTime` and a `DateTimeOffset` are converted to
  UTC, and a `DateTime` with `Kind.Unspecified` is taken as UTC, so a date comes back from a search in UTC. A
  `TermQuery`, `TermsQuery` or `PrefixQuery` with a date value matches that text on a keyword field. Dynamic mapping maps these strings as
  dates, which Elasticsearch rejects a prefix query on. Re-index documents indexed before this version, which keep the text
  the client wrote then.
- **`TermsQuery`** with more than 65,536 terms (`index.max_terms_count`) returns a failed `Result`.
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
`Bielu:BetterSearch:ElasticSearch:Indexes:articles:Mappings:0:Type`.

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
- **Fragments** use Elasticsearch's default size and count.
- **Fragments are HTML-escaped.** The document text is escaped (`<` becomes `&lt;`, `&` becomes `&amp;`), so the
  `<em>` tags are the only markup and a fragment is safe to render as HTML.
- **Invalid requests** return a failed `Result`: a `null` entry, an entry with neither `Field` nor
  `IsAllField`, two entries for the same field, also under different names for it (`Name` and `fields.Name`),
  and a `null` clause in a request's own `Queries`.

## When writes are searchable

By default a successful write is stored but not yet searchable: Elasticsearch makes it visible at the next index
refresh, about a second later unless `index.refresh_interval` is changed. Set `WriteVisibility` to
`WaitUntilSearchable` to have every index and remove call return only once a search can see the write:

```json
"SharedIndexSettings": { "ConnectionString": "http://localhost:9200", "WriteVisibility": "WaitUntilSearchable" }
```

- `IndexDocumentAsync`, `IndexMultipleDocumentsAsync`, `RemoveDocumentAsync` and `RemoveDocumentsAsync` send
  `refresh=wait_for`, so the call waits for the next scheduled refresh rather than forcing one.
- `RemoveAllDocumentsAsync` sends `refresh=true`, because delete by query can't wait for a refresh; it refreshes
  the index once the documents are removed.
- **Cost**: each call can take up to the refresh interval. Core's `IndexingServiceAsync.IndexMultipleDocumentsAsync`
  indexes one document at a time, so a batch through the service waits once per document. Leave it at `Default`
  for bulk loads and turn it on where a write must be visible straight away, such as tests and small sites.
- Waiting doesn't change what a failure means: a failed or cancelled call may still have stored the write.

## Removing documents

`DeleteDocumentRequest.Type` is the **index name**, not the document type held in `SearchDocument.Type`.

- **A document that is not there**: `RemoveDocumentAsync` succeeds, as the document is already gone.
- **An index that does not exist**: `RemoveDocumentAsync` returns a failed `Result`.
- **Several documents**: `RemoveDocumentsAsync(index, ids)` sends one bulk request and returns how many
  documents it removed; an id that is not there is not counted. When items fail, the `Result` fails naming
  their ids, and the other documents are already removed. A missing index returns a failed `Result` naming the ids.
  A request cancelled through the `CancellationToken`, before or during the request, throws
  `OperationCanceledException`.

## Checking an index exists

`IndexExistsAsync` is success with `true` on a 200 and `false` on a 404. Any other answer, such as a 401, 403
or 5xx, or no answer at all, is a failed `Result`. Its message names the HTTP status when there was one, then
the server's error, or the transport's exception when the server sent none, and that exception is attached as
its cause. A request
cancelled through the `CancellationToken` throws `OperationCanceledException`. `EnsureIndexExistsAsync` returns
a failed check as it is rather than trying to create the index.

Index operations report what happened: creating an existing index is success with `false`, and any other
rejection, such as a 403, is a failed `Result` whose message names the HTTP status and the server's error.
`EnsureIndexExistsAsync` and `DeleteIndexAsync` succeed with `true`, and `EnsureIndexExistsAsync` is `true` too
when another caller created the index between its check and its create.

The [feature matrix](../feature-matrix.md) has the full detail.
