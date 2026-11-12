---
"bielu-bettersearch": patch
---

Every Elasticsearch and OpenSearch indexing method now throws `OperationCanceledException` when its `CancellationToken` is cancelled while the request is in flight (#79). With `ThrowExceptions` off, the clients report such a request as a response with no status code, so `IndexDocumentAsync`, `IndexMultipleDocumentsAsync`, `RemoveDocumentAsync`, `RemoveAllDocumentsAsync`, `EnsureIndexExistsAsync`, `DeleteIndexAsync` and `CreateIndexAsync` returned a failed `Result` built from the client's debug information instead; only `IndexExistsAsync` and `RemoveDocumentsAsync` threw. A request that returns after its token was cancelled now throws too, even when the server answered, so a write cancelled after the server received it may still have been applied.
