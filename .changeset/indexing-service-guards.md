---
"bielu-bettersearch": patch
---

`IndexingServiceAsync` returns a failed `Result` instead of throwing, or passing the call to the provider, when it gets a `null` argument or a missing required value. `IndexMultipleDocumentsAsync(null)`, `RemoveDocumentAsync(null)` and `RemoveAllDocumentsAsync(null)` fail, and so do a document with a `null` or empty `Id` or `Index` (in `IndexDocumentAsync` and in a batch, before anything is sent), a `DeleteDocumentRequest` with a `null` or empty `Id` or `Type`, and a `DeleteAllDocumentsRequest` with a `null` or empty `IndexName`. Each error names what is missing. A validator that throws now fails with `Validation of document <id> failed: the validator threw <type>.`, the document's id in the `id` metadata and the exception as the cause, instead of the exception escaping the service. Cancellation still throws.
