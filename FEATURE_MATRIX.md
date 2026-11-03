# BetterSearch Provider Feature Matrix

This document describes the feature support matrix for each search provider.

## Query Types

| Feature | Elasticsearch | Meilisearch | Lifti |
|---------|:----------:|:-----------:|:-----:|
| **BoolQuery** (nested boolean logic) | ✅ | ❌ | ✅ |
| **TermQuery** (exact term match) | ✅ | ❌ | ✅ |
| **TermsQuery** (multiple exact terms) | ✅ | ❌ | ✅ |
| **FuzzyQuery** (approximate match) | ✅ | ❌ | ✅ |
| **PrefixQuery** (prefix match) | ✅ | ❌ | ✅ |
| **PrefixPhraseQuery** (phrase prefix match) | ✅ | ❌ | ✅ |
| **LuceneQuery** (raw Lucene syntax) | ✅ | ❌ | ❌ |
| **DateRangeQuery** | ✅ | ❌ | ❌ |
| **NumericRange** | ✅ | ❌ | ❌ |
| **LongRange** | ✅ | ❌ | ❌ |
| **StringRange** | ✅ | ❌ | ❌ |
| **SpatialSearchQuery** (geo/location) | ✅ | ❌ | ❌ |

## Query Combinators (Occurance)

| Feature | Elasticsearch | Meilisearch | Lifti |
|---------|:----------:|:-----------:|:-----:|
| **MUST** (AND) | ✅ | ❌ | ✅ |
| **SHOULD** (OR) | ✅ | ❌ | ✅ |
| **MUSTNOT** (AND NOT) | ✅ | ❌ | ✅ |
| **FILTER** | ✅ | ❌ | ❌ |

## Index Operations

| Feature | Elasticsearch | Meilisearch | Lifti |
|---------|:----------:|:-----------:|:-----:|
| Create Index | ✅ | ✅ | ✅ |
| Delete Index | ✅ | ✅ | ✅ |
| Index Exists | ✅ | ✅ | ✅ |
| Ensure Index Exists | ✅ | ✅ | ✅ |
| Index Document | ✅ | ✅ | ✅ |
| Index Multiple Documents | ✅ | ✅ | ✅ |
| Remove Document | ✅ | ✅ | ✅ |
| Remove All Documents | ✅ | ✅ | ✅ |

## Field Filtering

| Feature | Elasticsearch | Meilisearch | Lifti |
|---------|:----------:|:-----------:|:-----:|
| **Field-scoped queries** | ✅ | ❌ | ⚠️ |

> ⚠️ **Lifti field-scoped queries**: Lifti supports field-scoped queries via `FieldFilterQueryOperator`. The current implementation performs cross-field searches. Individual query translators do not filter by field name yet.

## Notes on the Meilisearch Provider

`Bielu.BetterSearch.Meilisearch` (the official MeiliSearch client, tested against Meilisearch 1.54) supports every index operation. Meilisearch applies writes asynchronously as tasks; the provider waits for each task, so a successful `Result` means the write was applied and a failed task returns Meilisearch's error. If the wait times out (`MeilisearchOptions.TaskTimeout`, 30 seconds by default) the `Result` fails but the task is not cancelled, so the write may still be applied later. Documents use `id` as the primary key, so an `Id` must contain only letters, digits, `-` and `_`.

Searching is not implemented yet: `SearchAsync` returns a failed `Result`, so no query type, combinator or field filter is supported. That is pending query translation, as for Elasticsearch. Once it lands, raw Lucene syntax (`LuceneQuery`) has no Meilisearch equivalent and will stay unsupported, and fuzzy matching will follow Meilisearch's typo tolerance rather than an edit distance per query.

## Notes on Unsupported Lifti Features

### Range Queries (DateRange, NumericRange, LongRange, StringRange)
Lifti is a full-text search engine focused on tokenized text. It does not have native support for range queries on numeric or date fields. For range-based filtering, consider using Elasticsearch or a dedicated search engine.

### Spatial / Geo Search (SpatialSearchQuery)
Lifti does not support geospatial indexing or proximity-based location queries. Use Elasticsearch for geospatial search requirements.

### Raw Lucene Query Syntax (LuceneSubQuery)
Lifti uses its own query syntax and parser. Raw Lucene query strings are not supported. Use specific query types (`TermSubQuery`, `PrefixSubQuery`, etc.) instead.

### FILTER Occurance
Lifti does not have a separate concept of a filter context (non-scoring). All queries in Lifti contribute to scoring.

## Test Coverage

All providers share the same base test suite (`SubQueryTranslationTestBase`, `IndexingTestBase`, `QueryResultBaseTests`) to ensure feature parity. Lifti-specific test overrides mark unsupported features with explanatory failure messages, documenting where Lifti intentionally diverges from the full feature set.
