---
"bielu-bettersearch": minor
---

Unify the document and result shapes before 1.0 (#164). Breaking: there are no `[Obsolete]` forwarders.

- **One document shape.** A search hit now holds the same `SearchDocument` that `GetDocumentAsync` returns and `IndexDocumentAsync` takes: `ISearchHit.Document` is a `SearchDocument`, with `Fields` (`IDictionary<string, IList<object>>`) instead of `CustomProperties` and `Type` instead of `ContentType`. Every provider now sets the hit's `Document.Index` to the query's `Index`, so a hit can be written back with no copy. `ISearchModel`, `BaseSearchModel` and `IEntity<T>` are removed. On Lifti, a hit's `Type` stays `null`, since Lifti keeps no type.
- **`Bielu.BetterSearch.Models` is merged into `Bielu.BetterSearch.Abstractions`.** `SearchDocument`, `DeleteDocumentRequest` and `DeleteAllDocumentsRequest` move from the `Bielu.BetterSearch` namespace to `Bielu.BetterSearch.Abstractions.Models`, and the `Bielu.BetterSearch.Models` package is no longer published. Remove its package reference and add `using Bielu.BetterSearch.Abstractions.Models;`.
- **Read-only results.** `ISearchResult.Items` is an `IReadOnlyList<ISearchHit>`, and `Items` and `Pagination` have no setters. `IPagination` is get-only. `BaseSearchResults` and `Pagination` use `init` setters, and `BaseSearchResults` starts with no items and an empty `Pagination` instead of `null`.
- **One way to read nested facet results.** Nested results are read from `IBucket.Aggregations` only: `IBucket.SubBuckets`, `IAggregation.NestedAggregation` (on `BucketAggregation` and `SingleAggregation`) and `KeyBucket.IsComplex` are removed, and `IBucket.Keys` moves to `KeyBucket` only, as `SingleBucket` had nothing to put in it. Aggregation and bucket members are get-only on the interfaces and `init` on the classes, and `Buckets` and `Keys` are `IReadOnlyList`s.
- **`IResultMapper<T>.MapResult` takes the query**: `MapResult(T searchEngineResult, ISearchQuery query)`, so a mapper can set each hit's index and the page. The internal Lifti mapper is renamed from `LiftiBaseSearchModelMapper` to `LiftiResultMapper`.
- The search result cache now keeps each hit's `Index`. An entry cached by an earlier version, without one, gives its hits the index of the query it was cached for.
