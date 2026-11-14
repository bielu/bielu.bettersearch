---
"bielu-bettersearch": minor
---

Add `SortOrderField`, an `ISortOrderField` implementation for `ISearchQuery.SortOrder`, so callers no longer write their own (#95). Every provider now checks `SortOrder` through one shared helper before it searches: a `null` entry returns a failed `Result` with `SortOrder holds a null entry.`, and an entry whose `FieldName` is `null`, empty or blank returns `A SortOrder entry needs a FieldName.`. Before, Elasticsearch and OpenSearch threw `NullReferenceException` out of `SearchAsync` for a `null` entry or a `null` name and sent an empty or blank name on to the engine as `fields.<name>`, Meilisearch failed with its own message for a missing name and threw for a `null` entry, and Lifti ignored them. Lifti still ignores a valid `SortOrder`. Breaking: the unused `Bielu.BetterSearch.Abstractions.Models.Sort` class is removed; the placeholder `EndpointApi` `SearchRequest.Sorts` now holds `SortOrderField`s.
