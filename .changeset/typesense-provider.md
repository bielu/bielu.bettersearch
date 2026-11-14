---
"bielu-bettersearch": minor
---

Add `Bielu.BetterSearch.Typesense`, a Typesense provider registered with `AddTypesense()`, configured from `Bielu:BetterSearch:Typesense` with one or more node URLs and an API key. It talks to Typesense's HTTP API directly and supports every index operation; writes are searchable when they return. It searches with term, terms, prefix, phrase prefix, numeric, long and date range queries, geo distance on the field named by `TypesenseOptions.GeoField`, nested boolean logic, paging (up to 250 hits a page), sorting (up to 3 fields holding one value), post filters and `Properties`, translated to Typesense filters. Fuzzy, raw Lucene and string range queries, a `MUSTNOT` range, prefix or spatial query, facets and highlighting return a failed `Result`. The examples AppHost now also runs Typesense 29.0. In the shared test bases, `IndexingTestBase.CreateDocumentRejectedByEngine` may return `null` to skip the test of a document the engine rejects.
