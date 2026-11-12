---
"bielu-bettersearch": minor
---

Elasticsearch: the client factory keys its clients on the connection string and authentication an index resolves to, as the OpenSearch provider does, instead of on the index name. Indexes with the same connection share one client, index names match whatever their case, an index named `default` can no longer collide with the shared client, and a changed connection string or password in `IOptionsMonitor` gets a new client on the next call instead of after a restart. Client creation no longer caches an exception, so invalid settings fail each call until they are fixed. Resolving an index's settings never writes into the stored options.

Breaking: `IElasticSearchClientSettingsManager.GetOrCreateClientSettings` is replaced by `GetConnectionSettings(indexName)`, which returns the merged settings as a new `ElasticSearchIndexSettings`, and `CreateClientSettings(settings)`. `ElasticsearchClientFactory` no longer takes `IOptionsMonitor<ElasticSearchOptions>`. `ElasticSearchIndexSettings.AuthenticationType` is now nullable: an `Indexes` entry that leaves it unset takes the shared authentication, including an entry with its own `ConnectionString`, which before used no authentication. Set `"AuthenticationType": "NONE"` on the entry to keep the old behavior.
