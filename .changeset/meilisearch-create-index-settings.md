---
"bielu-bettersearch": patch
---

Fix Meilisearch `CreateIndexAsync` leaving a new index without the provider's settings. When the index had been deleted outside the process within `MeilisearchOptions.SettingsCheckInterval`, the create trusted the earlier settings check, so searches on the new index failed until a later write. A successful create now always applies the settings.
