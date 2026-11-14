---
"bielu-bettersearch": minor
---

Remove dead and placeholder public API before 1.0 (#97). Breaking, for code that used any of these; nothing in the library read them:

- `Bielu.BetterSearch.Abstractions.Configuration.IndexSettings` is removed, with `IndexName`, `AliasName`, `TypeName`, `IndexingEnabled` and `DevMode`. `ElasticSearchIndexSettings`, `OpenSearchIndexSettings`, `MeilisearchIndexSettings` and `TypesenseIndexSettings` no longer derive from it. `IndexingEnabled = false` never turned indexing off; configuration that still sets these keys is ignored by the binder, as before.
- `ISearchQuery.PreviewAt` and `SearchQuery.PreviewAt` are removed, so they are no longer part of the cache key either.
- `BaseFacet`, `HighLighter`, the `Bielu.BetterSearch.Abstractions.Models.MultiTermBucket` class and `SearchSubQuery` are removed. Multi-term facet buckets are still returned as `KeyBucket`s.
- `ICreatableSearchQuery<TKey, TValue>` is now the non-generic `ICreatableSearchQuery` with `Add(Occurance key, ISearchSubQuery value)`. `ISearchQuery` and `INestableQuery` extend it as before.
- Core: `SearchConfiguration`, `BaseSearchConstants`, `TaskExtensions.SelectAsync` and the obsolete `Unsubscriber<T>` are removed.
- Lifti: the empty `LiftiSearchModelExtensions` class and `LiftiIndexingProviderAsync.RemoveAllDocumentsAsync(CancellationToken)`, which threw `NotImplementedException`, are removed. `RemoveAllDocumentsAsync(DeleteAllDocumentsRequest, CancellationToken)` is unchanged.
- The `Bielu.BetterSearch.EndpointApi` project, which held empty controller classes and was never packed, is deleted.
- Implementation types are now `internal`, as OpenSearch's already were. Elasticsearch: the search and indexing providers, `ElasticSearchClientSettingsManager` and `IElasticSearchClientSettingsManager`, `ElasticsearchClientFactory`, `ElasticSearchResultMapper` and `ElasticSearchQueryTranslateService`. Lifti: the search and indexing providers, `LiftiIndexManager`, `LiftiClientFactoryAsync`, `LiftiQueryTranslatorService`, `LiftiBaseSearchModelMapper` and every sub-query translator; `ILiftiIndexManager` stays public, as a documented extension point. Meilisearch: the search and indexing providers. Typesense: the search and indexing providers, `TypesenseHttpClientWrapper`, `TypesenseResponse`, `TypesenseSearchRequest` and `TypesenseSchema`. Resolve the providers through `ISearchProviderAsync` and `IIndexingProviderAsync`; the options, field mapping and authentication types and the `Add…` registration extensions stay public.
