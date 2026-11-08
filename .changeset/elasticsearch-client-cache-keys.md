---
"bielu-bettersearch": patch
---

Elasticsearch: resolving an index without its own connection string no longer writes the shared URL into the stored options, index names reach the same cached client whatever their case, and an index named `default` no longer collides with the shared client. Adds `IElasticSearchClientSettingsManager.GetOrCreateSharedClientSettings`.
