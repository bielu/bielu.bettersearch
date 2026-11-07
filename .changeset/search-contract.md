---
"bielu-bettersearch": minor
---

Fix the search contract before facets and highlighting (#17). `ISearchServiceAsync.SearchAsync`, `ISearchProviderAsync.SearchAsync` and `IDocumentValidatorAsync.ValidateDocumentAsync` take a `CancellationToken`, and every provider passes it on: a cancelled search throws `OperationCanceledException`. `ISearchResult.Items` are now `ISearchHit`s, each with the `Document` and its `Score`. `BaseSearchModel.Id` returns the id unchanged (`Up-1` stays `Up-1`), so `Key` is gone. The fluent API gains `FilterByCulture`, a `FILTER` on the plain `Culture` field written with `CultureField.Value`.

Breaking: `ISearchQuery<T>` and `ISearchResult<T>` lose their unused generic; `Score` moves from `ISearchModel`, `BaseSearchModel` and `SearchDocument` to the hit; `IEntity<T>` declares `Id`; `ISearchModel` and `BaseSearchModel` drop `Key`, `Url`, `SearchCulture`, `ViewModelType`, `Facet` and `AdditionalKeys`; `IPagination` and `Pagination` drop `TotalNumberOfPages`, `SiteId`, `ListingId`, `BaseUrl` and `QueryUrl`.
