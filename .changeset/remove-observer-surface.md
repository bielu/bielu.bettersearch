---
"bielu-bettersearch": minor
---

Remove the observer surface from the search and indexing services (#165). Breaking:

- `IIndexingServiceAsync` no longer extends `IObservable<SearchDocument>`, `IObservable<DeleteDocumentRequest>`, `IObservable<DeleteAllDocumentsRequest>` or `IDisposable`, and `ISearchServiceAsync` no longer extends `IObservable<ISearchModel>` or `IDisposable`. Calls to `Subscribe` and `Dispose` on them no longer compile, and a `using` declaration of a service no longer compiles.
- `IndexingServiceAsync` and `SearchServiceAsync` lose `Subscribe`, `Dispose()` and `protected virtual Dispose(bool)`. A subclass that overrides `Dispose(bool)` no longer compiles; a custom service set through `SetIndexingServiceType<T>()` or `SetSearchServiceType<T>()`, or a decorator, no longer has to implement `Subscribe` or `Dispose`.
- `Unsubscriber<T>`, already `[Obsolete]`, is deleted.
- The routing services and the caching decorators no longer forward or send notifications.

Code that needs to act on a write or a search decorates the service in DI, as the search cache does; see "Run code after a write" in the getting started guide. The OpenTelemetry spans and metrics cover the observability use. A search's `hit_count` span tag is now counted from the returned hits when a listener records the span, rather than while observers were notified.
