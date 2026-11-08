---
"bielu-bettersearch": patch
---

Setting `SearchDocument.Fields` to `null`, in code or by deserializing, now gives an empty dictionary, so `IndexingServiceAsync` indexes such a document as one with no fields instead of failing. Elasticsearch, OpenSearch and Meilisearch now map hit fields to `CustomProperties` through one shared helper, so all three return the same CLR types (`string`, `long`, `double`, `bool`) for the same stored value; OpenSearch now converts `JsonElement` values as the other two do.
