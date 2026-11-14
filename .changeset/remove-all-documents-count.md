---
"bielu-bettersearch": minor
---

`IIndexingServiceAsync.RemoveAllDocumentsAsync` now returns the number of documents the provider removed (#87). Before, `IndexingServiceAsync` dropped the provider's count, so a successful removal returned `0`; a failed removal was already a failed `Result` and still is. Breaking: the method returns `Result<long?>` instead of `Result<int>`, matching `IIndexingProviderAsync.RemoveAllDocumentsAsync`, so callers reading `Value` as an `int` and custom `IIndexingServiceAsync` implementations must change. A `null` value means the engine removed the documents but did not report how many; Meilisearch returns `null` when its task details hold no `deletedDocuments` count.
