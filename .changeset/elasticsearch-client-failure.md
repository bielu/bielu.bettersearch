---
"bielu-bettersearch": patch
---

Every Elasticsearch indexing method now returns a failed `Result` instead of throwing when the client cannot be created, for example on a bad connection string (#54). The failed `Result` keeps the original exception as its cause, and `IndexExistsAsync` and `CreateIndexAsync` now keep the cause of a failed request too. A token that is already cancelled when an indexing method is called now throws `OperationCanceledException` from every method, where `IndexExistsAsync` and `CreateIndexAsync` used to swallow it and some methods returned a failed `Result`. A token cancelled while a request is in flight can still come back as a failed `Result`, because the Elasticsearch transport reports it as a failed response.
