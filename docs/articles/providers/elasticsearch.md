# Elasticsearch

[Bielu.BetterSearch.ElasticSearch](https://www.nuget.org/packages/Bielu.BetterSearch.ElasticSearch/) runs
queries on an [Elasticsearch](https://www.elastic.co/elasticsearch) cluster, with paging, sorting, post
filters and scores.

> [!WARNING]
> **What won't work**
>
> - **Facets and highlighting** can be expressed, but a query that asks for them returns a failed `Result`.
> - **Index operations report odd values**: creating an index the cluster rejects is success with `false`,
>   deleting an index returns `false` even when it worked, and `RemoveAllDocumentsAsync` reports a count of 0.
> - **Bulk indexing**: Core's `IndexingServiceAsync.IndexMultipleDocumentsAsync` indexes one document at a time,
>   so this provider's `BulkAsync` path is never used.
>
> Invalid or missing connection settings throw from searching; every indexing method turns them into a
> failed `Result`.

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
| `Indexes` | Per-index settings. An entry with its own `ConnectionString` gets its own client and uses only its own authentication; an entry without one uses the shared settings. Keys match index names whatever their case, so `People` applies to the index `people` |
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
- **Sorting on a text field** is rejected by Elasticsearch; sort on `Field.keyword`.
- **`SpatialSearchQuery`** needs the field mapped as `geo_point`; dynamic mapping doesn't create one.
- **`LuceneQuery`** field names need the `fields.` prefix.
- **`FuzzyQuery`** allows at most 2 edits, and uses `AUTO` when `Fuziness` is unset.
- **`TermsQuery`** with more than 65,536 terms (`index.max_terms_count`) returns a failed `Result`.
- **Total hit count** is exact up to `MaxResultWindow` matches, then a lower bound.

## Removing documents

`DeleteDocumentRequest.Type` is the **index name**, not the document type held in `SearchDocument.Type`.

- **A document that is not there**: `RemoveDocumentAsync` succeeds, as the document is already gone.
- **An index that does not exist**: `RemoveDocumentAsync` returns a failed `Result`.

## Checking an index exists

`IndexExistsAsync` is success with `true` on a 200 and `false` on a 404. Any other answer, such as a 401, 403
or 5xx, or no answer at all, is a failed `Result` carrying the request's debug information and, when there is
one, the original exception. `EnsureIndexExistsAsync` returns that failure rather than trying to create the
index.

The [feature matrix](../feature-matrix.md) has the full detail.
