---
"bielu-bettersearch": patch
---

`RemoveDocumentAsync` treats a document that is not there as removed in every provider (#30). Elasticsearch and OpenSearch used to return a failed `Result` for the 404 `not_found` answer; they now succeed, as Lifti and Meilisearch already did. A missing index is a failed `Result` in every provider: Lifti used to create it on demand and report success, and now fails like the others.
