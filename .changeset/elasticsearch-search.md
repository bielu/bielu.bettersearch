---
"bielu-bettersearch": minor
---

Implement search in the Elasticsearch provider. `SearchAsync` translates every sub-query type (bool, term, terms, prefix, prefix phrase, fuzzy, numeric, long, date and string ranges, raw Lucene, geo distance), applies `Page`, `PageSize`, `SortOrder`, `Properties` and post filters, and maps hits with their id, score, type and fields. Server errors and unsupported facets and highlighting return a failed `Result`. The translator is now `IQueryTranslateServiceAsync<Query>` with one `ISubQueryTranslator<Query>` per sub-query type, `ISearchModel` gains `Score`, and `SearchServiceAsync` returns a provider's failed `Result` instead of throwing.
