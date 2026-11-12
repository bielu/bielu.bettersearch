---
"bielu-bettersearch": patch
---

Every Elasticsearch indexing method now returns a failed `Result` with the exception as its cause when a request throws, for example a `TransportException` with `ThrowExceptions()` on (#71). `IndexDocumentAsync`, `IndexMultipleDocumentsAsync`, `RemoveAllDocumentsAsync` and `DeleteIndexAsync` used to throw it to the caller. `OperationCanceledException` still propagates.
