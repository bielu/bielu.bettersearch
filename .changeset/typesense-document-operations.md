---
"bielu-bettersearch": minor
---

Implement `GetDocumentAsync`, `CountAsync` and `UpdateDocumentAsync` on the Typesense provider, which returned a failed `Result` until now. `GetDocumentAsync` returns the fields as hits return them, and `null` for a missing document or collection; an id of `.`, `..`, `export` or `search` is read through an export filtered on that id. `CountAsync` returns the exact `found` of a search for no hits with the filter `SearchAsync` sends, post filter included; a missing collection is a failed `Result`, as a search there. `UpdateDocumentAsync` reads the document, then sends a Typesense partial update that replaces the named fields, removing every typed copy of a named field that the new value does not write, and keeps the others; it returns `false` for a missing document or collection and creates neither. The shared `DocumentOperationsTestBase` now runs on Typesense too.
