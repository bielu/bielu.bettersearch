---
"bielu-bettersearch": minor
---

`IIndexingServiceAsync` observers follow the `IObserver` contract (#56). The `SearchDocument`, `DeleteDocumentRequest` and `DeleteAllDocumentsRequest` streams now behave the same way: `OnNext` is sent once after each operation the provider completed successfully, a rejected document or failed operation sends nothing (the returned `Result` carries the reason), `OnError` is never sent, and `OnCompleted` is sent once, when `IndexingServiceAsync` is disposed. Before, a rejected document sent `OnCompleted` while later documents still sent `OnNext`, `OnNext` for an index or delete was sent before the provider ran, even when it then failed, and deletes sent `OnCompleted` after every call.
