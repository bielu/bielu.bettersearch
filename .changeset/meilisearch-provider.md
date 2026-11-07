---
"bielu-bettersearch": minor
---

Add `Bielu.BetterSearch.Meilisearch`, a Meilisearch provider registered with `AddMeilisearch()`. It supports every index operation and waits for each Meilisearch task, so a successful result means the write was applied (after a `TaskTimeout` the outcome is unknown). It searches with term, terms, prefix, phrase prefix, numeric, long, date and string range queries, nested boolean logic, paging, sorting, post filters and `Properties`, translated to Meilisearch filters; fuzzy, raw Lucene and spatial queries, facets and highlighting return a failed `Result`.
