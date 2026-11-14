---
"bielu-bettersearch": patch
---

Elasticsearch and OpenSearch: `RemoveAllDocumentsAsync` refreshes the index before its delete by query, so documents whose writes were acknowledged but not refreshed yet are removed too, whatever `WriteVisibility` is. This adds one request per call. A refresh that fails (for example a 403, a missing index or a failed shard) returns a failed `Result` and no delete is sent.
