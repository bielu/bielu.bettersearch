---
"bielu-bettersearch": patch
---

Fix searches throwing `ArgumentNullException` when a hit has a field stored as `null` or its whole `fields` stored as `null`. Elasticsearch, OpenSearch and Meilisearch now return such a field as an empty list in `CustomProperties`, and a document with `null` fields as one with no fields; Lifti and Meilisearch no longer throw when such a document is indexed through the provider. Elasticsearch, OpenSearch and Meilisearch also leave `null` values out of a field's list. `IndexingServiceAsync.IndexDocumentAsync` now returns a failed `Result` for a document whose `Fields` is `null`, a field whose value is `null` or a field holding a `null` value, so they can't be stored through the service, and `IndexMultipleDocumentsAsync` checks the whole batch, including `null` entries, before indexing any of it, completing the indexing observers when it rejects the batch.
