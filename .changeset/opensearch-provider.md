---
"bielu-bettersearch": minor
---

Add `Bielu.BetterSearch.OpenSearch`, an OpenSearch provider registered with `AddOpenSearch()`. It supports every index operation and searching: every query type, boolean occurrences, paging, sorting, post filters and `Properties`, translated the same way as the Elasticsearch provider. Facets and highlighting return a failed `Result`, as they do for Elasticsearch.
