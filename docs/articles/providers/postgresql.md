# PostgreSQL

`Bielu.BetterSearch.PostgreSql` runs queries on [PostgreSQL](https://www.postgresql.org/) full-text search, through
[Npgsql](https://www.npgsql.org/). It suits an application that already runs PostgreSQL and needs persistent search
over a few thousand documents without operating a search cluster, and it keeps the move to Elasticsearch or
OpenSearch a change of registration. Paging, sorting, post filters, `GetDocumentAsync`, `CountAsync` and
`UpdateDocumentAsync` work.

It needs PostgreSQL 16 or later, for `pg_input_is_valid`. The AppHost and the integration tests run the image
Aspire's PostgreSQL integration pins.

> [!WARNING]
> **What won't work**
>
> - **`LuceneQuery`** returns a failed `Result`: PostgreSQL has no Lucene syntax, and the provider doesn't
>   translate it.
> - **`FuzzyQuery`** needs the `fuzzystrmatch` extension (`CREATE EXTENSION fuzzystrmatch;`) in the database.
>   Without it, a search with a fuzzy query returns a failed `Result` that says so. The distance is Levenshtein's,
>   so a transposition (`jnae` for `jane`) counts as two edits, where Elasticsearch counts it as one.
> - **Relevance scores**: every query runs as a SQL condition, and every hit has the `Score` 1. `ts_rank` is not
>   used yet.
> - **Facets** and **highlighting** are not implemented yet and return a failed `Result`.
> - **`IndexPrefix`** has no PostgreSQL setting yet, and **index rebuilds** (`IIndexLifecycleServiceAsync`) return a
>   failed `Result`: the provider does not implement `IIndexLifecycleProviderAsync`.
> - **An index name** longer than 63 bytes, the longest name PostgreSQL keeps, makes each call for it return a
>   failed `Result`.
>
> A connection string that doesn't parse, or names no host, makes the host fail to start, and each search and
> write for that index return a failed `Result` whose message names the setting to fix and never the value.

## Install

```bash
dotnet add package Bielu.BetterSearch.PostgreSql --prerelease
```

## Configure

```csharp
using Bielu.BetterSearch.DepedencyInjection;
using Bielu.BetterSearch.PostgreSql.DepedencyInjection;

builder.Services.AddBetterSearch(builder.Configuration, search => search.AddPostgreSql());
```

```json
{
  "Bielu": {
    "BetterSearch": {
      "PostgreSql": {
        "SharedIndexSettings": {
          "ConnectionString": "Host=localhost;Database=app;Username=app;Password=..."
        },
        "Schema": "search",
        "TextSearchConfiguration": "simple"
      }
    }
  }
}
```

| Setting | Default | Meaning |
|---------|---------|---------|
| `SharedIndexSettings:ConnectionString` | none | The Npgsql connection string. Leave it empty to use the `NpgsqlDataSource` registered in the service collection, for example by `AddNpgsqlDataSource` or Aspire's `AddNpgsqlDataSource`. |
| `Indexes:<index>:ConnectionString` | the shared one | A connection string for one index. Index names match whatever their case. |
| `Schema` | `public` | The schema holding the tables, one per index. Any other schema is created by the first `CreateIndexAsync` when it is missing. |
| `TextSearchConfiguration` | `simple` | The text search configuration that splits string values into words: `simple` lower-cases them with no stemming, closest to Elasticsearch's standard analyzer; `english` stems them and drops stop words. It must be a plain name, optionally after a schema (`pg_catalog.english`). |

The provider creates one `NpgsqlDataSource`, which pools its connections, per distinct connection string, and
disposes it with the container. A data source taken from the container is not disposed by the provider.

## How documents are stored

Each index is a table in `Schema`, named exactly as the index (quoted, so the case is kept), created by
`CreateIndexAsync`, `EnsureIndexExistsAsync` or the first write to it:

| Column | Type | Holds |
|--------|------|-------|
| `id` | `text`, primary key | The document id |
| `type` | `text` | The document type |
| `fields` | `jsonb` | Every field as an array of its values |
| `search` | `tsvector`, generated | The words of every string value, under a GIN index |

Values keep their types through the round trip. An integer is stored exactly at any size, as `jsonb` numbers are
decimals; a `double`, `float` or `decimal` is written with a fraction (`3.0`), so it comes back as a `double`; a
date is stored as ISO 8601 text in UTC, as on the other providers. `NaN` and infinities fail the write, naming the
field, and a string holding the null character is rejected by PostgreSQL.

The `search` column is generated with the `TextSearchConfiguration` the table was created with. After changing
the setting, rebuild the existing indexes: until then, the GIN index holds words split the old way, and a query
whose words the new configuration splits differently can miss documents.

## How queries behave

Every value goes into the SQL as a parameter, and so does every field name, which the query reads with
`fields -> $n`; only the schema and index names are written into the SQL text, as quoted identifiers. A field
name can hold any character.

- **`TermQuery`** with a string matches one word of the field's string values, normalized by the text search
  configuration, so `JANE` matches `Jane Doe` under `simple`. A string the configuration splits into several
  words matches nothing, as an Elasticsearch term query does on a text field. A string that reads as a number or
  a boolean also matches that value. A number matches an equal number (`3` matches `3.0`), and a date the stored
  ISO text. **`TermsQuery`** matches any of its values, sent as two array parameters whatever their number.
- **`PrefixQuery`** matches a word that starts with the value. **`PrefixPhraseQuery`** matches the value's words
  in order and next to each other, the last as a prefix.
- **`FuzzyQuery`** matches when every word of the value is within the edit distance of a word of the field: the
  larger number of `Fuziness`, at most 2, or by word length as Elasticsearch's `AUTO` when unset. `PrefixLenght`
  letters must match exactly.
- **Ranges** match when a single value of the field is within both bounds, so a field holding `10` and `60` does
  not match 20 to 50. Numbers compare with numbers, exactly for `LongRange`. `DateRangeQuery` compares the instants
  of string values that start with an ISO 8601 date, a value without an offset taken as UTC. `StringRange`
  compares whole string values by their UTF-8 bytes (the `C` collation).
- **`SpatialSearchQuery`** works on any field holding `"lat,lon"` strings, as Elasticsearch accepts for a
  `geo_point`, or objects with `lat` and `lon` (or `lng`), by great-circle distance with Elasticsearch's mean
  Earth radius. It needs no extension; no index serves it, so every document holding the field is checked.
- **Boolean logic** follows Lucene: `SHOULD` beside `MUST` or `FILTER` changes nothing, and `MUSTNOT` on its own
  returns the complement. A document without the field matches the negation of any query on it.

Word queries are answered from the GIN index on `search` first, then checked against their field. Ranges, sorting
and spatial queries read each document's `fields`, which is fine for thousands of documents; for much more, add
an expression index on the fields you filter on, or move to a search engine.

**Sorting** orders by the field's numbers, then its dates, then its strings by their UTF-8 bytes, each by the lowest
value ascending and the highest descending, as Elasticsearch orders a field with several values. Documents without
the field come last, and ties are ordered by id, so pages never overlap. **Paging** uses `LIMIT` and `OFFSET`, and
the total is counted in the same snapshot as the page.

## Writes

Each write commits before it returns, so a successful write is searchable at once. `IndexMultipleDocumentsAsync`
writes every document of an index in one transaction, in inserts of up to 1000 documents: when PostgreSQL rejects
one, none of them is stored, and the error names every id of that index. When an id repeats in a batch, the last
document wins. `UpdateDocumentAsync` merges the named fields into the stored ones in one statement, so a
concurrent update of other fields is not lost.

## Local development

The examples AppHost runs PostgreSQL with a database named `bettersearch`, and [pgweb](https://sosedoff.github.io/pgweb/)
for browsing the tables:

```bash
dotnet run --project src/examples/Bielu.BetterSearch.Examples.AppHost
```

The integration tests start the same server through the AppHost, so they need Docker, and install
`fuzzystrmatch` in the test database.
