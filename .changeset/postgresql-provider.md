---
"bielu-bettersearch": minor
---

Add `Bielu.BetterSearch.PostgreSql`, a provider on PostgreSQL full-text search through Npgsql, registered with `AddPostgreSql()`. Each index is a table with the fields as `jsonb` and a generated `tsvector` under a GIN index, created by `CreateIndexAsync` or the first write. Term, terms, prefix, prefix phrase, fuzzy (with the `fuzzystrmatch` extension), numeric, long, date and string range, spatial and nested bool queries translate into parameterized SQL with Lucene bool semantics; raw Lucene queries, facets and highlighting return a failed `Result`. Paging, sorting, post filters, `GetDocumentAsync`, `CountAsync` and `UpdateDocumentAsync` work, a bulk write is one transaction per index, and an index without a connection string uses the `NpgsqlDataSource` registered in the container. The examples AppHost runs PostgreSQL with pgweb.
