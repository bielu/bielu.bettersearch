---
"bielu-bettersearch": patch
---

Lifti's `RemoveDocumentAsync` now matches Elasticsearch on errors (#53): a cancelled token throws `OperationCanceledException` instead of returning a failed `Result`, and a failed `Result` keeps the original exception as its cause. The index to remove from now comes from the registered `IClientFactoryAsync<IFullTextIndex<string>>`, like every other Lifti write, so a custom or decorated factory is honoured. `ILiftiIndexManager.GetIndexAsync` is now a required member with no default implementation; a custom index manager must implement it (return `null` for a missing index, without creating it).
