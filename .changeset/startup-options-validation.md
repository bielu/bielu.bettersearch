---
"bielu-bettersearch": patch
---

Elasticsearch, OpenSearch and Meilisearch now validate their connection settings at startup (#105). Each provider's `IValidateOptions` runs the same shared checks the clients run per call, on `SharedIndexSettings` and on every `Indexes` entry after merging, so a host with a bad connection string, missing credentials or an unknown authentication type fails to start with `OptionsValidationException`, listing every failure, each naming its configuration path, such as `Bielu:BetterSearch:OpenSearch:Indexes:people:ConnectionString`. A missing or misspelled section is one failure naming the section. The shared settings are not checked when they set no connection and every `Indexes` entry sets its own.

`MaxResultWindow` (Elasticsearch, OpenSearch) and `MaxTotalHits` (Meilisearch) must be greater than 0, and Meilisearch's `TaskTimeout` and `SettingsCheckInterval` must not be negative and its `GeoField` and `PhraseFields` must be names a filter can hold; these also fail startup. Meilisearch now registers its options with `ValidateOnStart`, as the other two do. A `MaxTotalHits` of 0 or less now fails a Meilisearch search with a message naming the setting.

The validators also run whenever the options are read, so after a reload to invalid settings `SearchAsync` and the Meilisearch writes return a failed `Result` with the validator's messages instead of throwing `OptionsValidationException`. Messages changed: a per-index connection failure found by the validator names that index (`Indexes:people:ConnectionString`) rather than `Indexes:<index>`, an empty configuration reports `No <section> settings were found.`, and the `MaxResultWindow` message is now `Bielu:BetterSearch:<Provider>:MaxResultWindow is 0, but it must be greater than 0.`
