# OpenSearch

[Bielu.BetterSearch.OpenSearch](https://www.nuget.org/packages/Bielu.BetterSearch.OpenSearch/) runs queries
on an [OpenSearch](https://opensearch.org/) cluster. It translates queries the same way as the
[Elasticsearch provider](elasticsearch.md), so the same queries give the same results, with paging, sorting,
post filters and scores.

> [!WARNING]
> **What won't work**
>
> - **Facets and highlighting** can be expressed, but a query that asks for them returns a failed `Result`.
> - **Bulk indexing**: `IndexMultipleDocumentsAsync` indexes one document at a time.
> - **Remove all documents** reports a count of 0.

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
- **`StringRange` bounds are case-sensitive.** They are compared to the indexed tokens as given, so on an
  analyzed field, whose tokens are lower-cased, `"a"`–`"z"` matches `Jane` but `"A"`–`"Z"` doesn't. Pass
  lower-case bounds, or use `Field.keyword` to compare whole values.
- **Sorting on a text field** is rejected by OpenSearch; sort on `Field.keyword`.
- **`SpatialSearchQuery`** needs the field mapped as `geo_point`; dynamic mapping doesn't create one.
- **`LuceneQuery`** field names need the `fields.` prefix.
- **`FuzzyQuery`** allows at most 2 edits, and uses `AUTO` when `Fuziness` is unset.
- **Total hit count** is exact up to `MaxResultWindow` matches, then a lower bound.

Unlike Elasticsearch, index operations report what happened: creating an existing index is success with
`false`, and any other rejection, such as a 403, is a failed `Result`. The
[feature matrix](../feature-matrix.md) has the full detail.
