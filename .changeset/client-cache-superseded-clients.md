---
"bielu-bettersearch": patch
---

Elasticsearch and OpenSearch: the client factories keep only the client each index name currently resolves to. When a password, API key or token rotates through `IOptionsMonitor`, the client for the old credential is dropped from the cache once no index name resolves to it, instead of staying cached for the life of the application. An index name not used for an hour is forgotten, so short-lived index names do not accumulate or keep old clients cached. A dropped client is not disposed, so requests already running on it finish. Switching back to an earlier setting reuses its client only while another index name still uses that setting; otherwise it creates a new client, and the `configureClient` callbacks run again.
