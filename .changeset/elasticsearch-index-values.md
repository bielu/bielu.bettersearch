---
"bielu-bettersearch": patch
---

Elasticsearch `DeleteIndexAsync` and `EnsureIndexExistsAsync` now succeed with `true`, where they used to succeed with `false` (#69). Elasticsearch `CreateIndexAsync` now returns `false` without logging an error when the index already exists, and a failed `Result` naming the HTTP status and the server's error for any other rejection, such as a 403, where it used to report every rejection as a successful `false`; a cancelled create or delete throws `OperationCanceledException`. On Elasticsearch and OpenSearch, `EnsureIndexExistsAsync` is now `true` when another caller creates the index between its check and its create, as on Meilisearch, instead of `false`. The shared index tests now assert the returned values, so every provider has to agree.
