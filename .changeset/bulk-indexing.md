---
"bielu-bettersearch": minor
---

`IndexingServiceAsync.IndexMultipleDocumentsAsync` validates the whole batch before indexing any of it, then makes one call to the provider's bulk `IndexMultipleDocumentsAsync` instead of indexing one document at a time. A document the validator rejects now stores none of the batch, where the documents before it used to be stored already, and the error names the document. When the batch fails, the indexing observers get nothing, even for documents the engine stored; they used to get `OnNext` for each document stored before the failure. Every provider now names the documents an engine rejects, in the error's `ids` metadata: Elasticsearch and OpenSearch read each item of the bulk response, so the other documents stay stored, and Elasticsearch returns the real count instead of `0`; Meilisearch and Lifti write each index's documents as one change, so none of that index's documents is stored. Lifti adds each index's documents with one `AddRangeAsync`.
