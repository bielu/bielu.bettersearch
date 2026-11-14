# OpenSearch

[Bielu.BetterSearch.OpenSearch](https://www.nuget.org/packages/Bielu.BetterSearch.OpenSearch/) runs queries
on an [OpenSearch](https://opensearch.org/) cluster. It translates queries the same way as the
[Elasticsearch provider](elasticsearch.md), so the same queries give the same results, with paging, sorting,
post filters, highlighting and scores.

> [!WARNING]
> **What won't work**
>
> - **Facets** can be expressed, but a query that asks for them returns a failed `Result`.
> - **Bulk indexing**: Core's `IndexingServiceAsync.IndexMultipleDocumentsAsync` indexes one document at a time,
>   so this provider's `BulkAsync` path is never used.
> - **Remove all documents** reports a count of 0.
>
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
| `MaxResultWindow` | Deepest result a search may page to, 10,000 by default; match it to the indexes' `index.max_result_window`. It must be greater than 0, or every search fails with a configuration error |

You can also set the options in code with `services.Configure<OpenSearchOptions>(...)`.

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
- **Sorting on a text field** is rejected by OpenSearch; sort on `Field.keyword`.
- **`SpatialSearchQuery`** needs the field mapped as `geo_point`; dynamic mapping doesn't create one.
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

As on Elasticsearch, index operations report what happened: creating an existing index is success with
`false`, and any other rejection, such as a 403, is a failed `Result`. `EnsureIndexExistsAsync` and
`DeleteIndexAsync` succeed with `true`, and `EnsureIndexExistsAsync` is `true` too when another caller created
the index between its check and its create. The
[feature matrix](../feature-matrix.md) has the full detail.
