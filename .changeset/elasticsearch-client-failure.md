---
"bielu-bettersearch": patch
---

Every Elasticsearch indexing method now returns a failed `Result` instead of throwing when the client cannot be created, for example on a bad connection string (#54). The failed `Result` keeps the original exception as its cause, and `IndexExistsAsync` and `CreateIndexAsync` now keep the cause of a failed request too. A cancelled token throws `OperationCanceledException` from every indexing method, where `IndexExistsAsync` and `CreateIndexAsync` used to swallow it and some methods returned a failed `Result`.
