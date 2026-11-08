---
"bielu-bettersearch": patch
---

`RemoveDocumentAsync` treats a document that is not there as removed in every provider (#30). Elasticsearch and OpenSearch used to return a failed `Result` for the 404 `not_found` answer; they now succeed, as Lifti and Meilisearch already did. A missing index is a failed `Result` in Elasticsearch, OpenSearch and Meilisearch. Lifti keeps indexes in memory, so it still reports success for a missing index, but no longer creates the index as a side effect. Elasticsearch's `RemoveDocumentAsync` now returns a failed `Result` instead of throwing when the client cannot be created, keeping the exception as the cause; cancellation still throws.
