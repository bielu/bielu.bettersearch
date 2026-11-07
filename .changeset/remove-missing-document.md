---
"bielu-bettersearch": patch
---

`RemoveDocumentAsync` treats a document that is not there as removed in every provider (#30). Elasticsearch and OpenSearch used to return a failed `Result` for the 404 `not_found` answer; they now succeed, as Lifti and Meilisearch already did. A missing index is still a failed `Result` in Elasticsearch and OpenSearch.
