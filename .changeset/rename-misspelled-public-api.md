---
"bielu-bettersearch": minor
---

Rename the misspelled and misleading public API before 1.0 (#96). Breaking: there are no `[Obsolete]` forwarders, so code that uses the old names stops compiling until it is updated.

- `Occurance` is now `Occurrence`, for the enum and every `Occurance` property (`ISearchSubQuery.Occurrence`, `SearchQuery.Add(Occurrence, ...)` and the rest). Its members are `Must`, `Should`, `MustNot` and `Filter`, which were `MUST`, `SHOULD`, `MUSTNOT` and `FILTER`. The numeric values are unchanged.
- The `DepedencyInjection` namespaces are now `DependencyInjection`: `Bielu.BetterSearch.DependencyInjection` in Core and Lifti, and `Bielu.BetterSearch.<Provider>.DependencyInjection` in the Elasticsearch, OpenSearch, Meilisearch, Typesense and PostgreSQL providers. The `Add…` extensions keep their names, so most consumers only change a `using`.
- Elasticsearch `AuthenticationType` members are now `None`, `Cloud`, `CloudApi`, `Password`, `ApiKey` and `Bearer`, and OpenSearch `OpenSearchAuthenticationType` members are `None`, `Password` and `ApiKey`. Configuration that sets the old upper-case values, such as `"AuthenticationType": "PASSWORD"`, still binds, since the configuration binder ignores case; validation messages now print the new names.
- `FuzzyQuery.PrefixLenght` is now `FuzzySubQuery.PrefixLength`. `FuzzyQuery.Fuziness`, a `Tuple<int, int>` of which every provider used only the larger number, is now `int? MaxEdits`; `null` still means `AUTO`. In the fluent API, `FuzzyQueryConfigurator.WithFuzziness(maxEdits)` is now `WithMaxEdits(maxEdits)`, and `WithFuzziness(low, high)` is removed.
- Query types now all end in `SubQuery`: `FuzzyQuery` is `FuzzySubQuery`, `DateRangeQuery` is `DateRangeSubQuery`, `SpatialSearchQuery` is `SpatialSearchSubQuery`, `BaseRangeQuery` is `BaseRangeSubQuery`, and `NumericRange`, `LongRange` and `StringRange` are `NumericRangeSubQuery`, `LongRangeSubQuery` and `StringRangeSubQuery`. Error messages that name the type use the new names. `LuceneSubQuery` moves from `LuceneQuery.cs` to `LuceneSubQuery.cs`.
- `MultiTermAggreation` is now `MultiTermAggregation`.
- `CreateQueryHelper.CreateGoeDistanceQuery` is now `CreateGeoDistanceQuery`.
- `HighLighterConfigurator` is now `HighlighterConfigurator`.
- `DeleteDocumentRequest.Type`, which held the index name, is now `DeleteDocumentRequest.Index`, and the `type` parameter of `IIndexingProviderAsync.RemoveDocumentAsync(string id, string type, ...)` is now `index`, as `RemoveDocumentsAsync` already named it. `IndexingServiceAsync.RemoveDocumentAsync` now fails a request with no index with "has a null or empty Index." instead of "has a null or empty Type, the name of the index.".
- The internal `ElasticsearchClientFactory` is now `ElasticSearchClientFactory`, matching the casing of the rest of the Elasticsearch package.
