---
"bielu-bettersearch": patch
---

Elasticsearch and OpenSearch: the client factories keep only the client each index name currently resolves to. When a password, API key or token rotates through `IOptionsMonitor`, the client for the old credential is dropped from the cache once no index name resolves to it, instead of staying cached for the life of the application. A dropped client is not disposed, so requests already running on it finish. Switching back to an earlier setting creates a new client rather than reusing the old one.
