---
"bielu-bettersearch": minor
---

Make the default interface members abstract before 1.0 (#166). Breaking for third-party implementers: a provider, service or decorator that relied on one of these defaults no longer compiles until it implements the member. Before, it compiled and returned a failed `Result` (or an empty value) when called, so a decorator written before `CountAsync` existed answered "does not support CountAsync" instead of forwarding to the service it wraps.

- `ISearchProviderAsync.GetDocumentAsync` and `ISearchProviderAsync.CountAsync` are abstract.
- `ISearchServiceAsync.GetDocumentAsync` and `ISearchServiceAsync.CountAsync` are abstract. A decorator of the search service must forward them.
- `IIndexingProviderAsync.UpdateDocumentAsync` is abstract. A provider that can't update fields implements it with a failed `Result`, as Lifti does.
- `IIndexingServiceAsync.UpdateDocumentAsync` is abstract.
- `ISearchResult.Aggregations`, `ISearchHit.Highlights` and `IBucket.Aggregations` are abstract get-only properties. `BaseSearchResults`, `SearchHit`, `KeyBucket` and `SingleBucket` already implement them.
- `ISearchProviderAsync.PingAsync` keeps its default implementation, as an optional capability: a provider that does not implement it fails the ping, saying the engine can't be checked, so the health check reports the registration's failure status.

Every provider, service and null implementation in this repository already implemented these members, so their behavior does not change. New members added to these interfaces after 1.0 get a default implementation again.
