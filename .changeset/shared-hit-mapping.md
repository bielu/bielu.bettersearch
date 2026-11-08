---
"bielu-bettersearch": patch
---

Setting `SearchDocument.Fields` to `null`, in code or by deserializing, now gives an empty dictionary, so `IndexingServiceAsync` indexes such a document as one with no fields instead of failing. Elasticsearch, OpenSearch and Meilisearch now map hit fields to `CustomProperties` through the same code, and all three return the same CLR types (`string`, `long`, `double`, `bool`) for the same stored value. Meilisearch now stores a whole `double` such as `3.0` with its fraction, so it comes back as a `double` instead of a `long`, as on Elasticsearch and OpenSearch; documents indexed before this change keep returning a `long` until they are indexed again.
