---
"bielu-bettersearch": minor
---

Meilisearch's `IndexMultipleDocumentsAsync` adds each index's documents in tasks of at most the new `BulkBatchSize` index setting (1000 by default, in `SharedIndexSettings` or an `Indexes` entry), one after another, so a large import stays under the server's payload size limit; it used to send them all in one task. A batch split into several tasks is no longer all or nothing: the first task that fails stops the import, the tasks before it stay stored, and the error's `ids` metadata holds every document not confirmed as stored, while its message separates the documents already stored, the failed task's (stored none, or may still index them after a `TaskTimeout`, with its `taskUid`) and the ones not sent. A batch that fits in one task behaves as before. A `BulkBatchSize` of 0 or less fails startup validation, which the Meilisearch options now run (`ValidateOnStart`).
