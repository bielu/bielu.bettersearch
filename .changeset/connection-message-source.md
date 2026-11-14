---
"bielu-bettersearch": patch
---

A connection setting failure for an `Indexes` entry now names only the setting that changes its value, in Elasticsearch, OpenSearch, Meilisearch and Typesense: the entry's own path when it sets the value, and the `SharedIndexSettings` path, with the entry that inherits it, when it does not. An entry takes the authentication details only with its own `AuthenticationType`, so a failure for the shared credentials now names both settings as the one-index alternative.
