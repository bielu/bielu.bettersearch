---
"bielu-bettersearch": patch
---

Fix searches throwing `ArgumentNullException` when a hit has a field stored as `null`. Elasticsearch, OpenSearch and Meilisearch now return such a field as an empty list in `CustomProperties`, and Lifti and Meilisearch no longer throw when such a document is indexed through the provider. `IndexingServiceAsync.IndexDocumentAsync` now returns a failed `Result` naming the fields whose value is `null`, so they can't be stored through BetterSearch.
