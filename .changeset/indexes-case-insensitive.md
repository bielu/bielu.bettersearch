---
"bielu-bettersearch": patch
---

Fix per-index settings being skipped when the `Indexes` key's case differs from the index name. The configuration binder filled `Indexes` as a case-sensitive dictionary, so `Indexes:People` never applied to the index `people` and its requests silently went to the shared server with the shared credentials. `Indexes` on `ElasticSearchOptions`, `OpenSearchOptions` and `MeilisearchOptions` now matches index names whatever their case, whether the options come from configuration or are set in code: a dictionary assigned to `Indexes` is copied into a case-insensitive one.
