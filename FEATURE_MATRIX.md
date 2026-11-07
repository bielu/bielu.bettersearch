# BetterSearch Provider Feature Matrix

What each provider really does today. This is a beta: see [ROADMAP.md](./ROADMAP.md) for the plan.

✅ works · ⚠️ works with the caveat noted · ❌ not available

Elasticsearch and OpenSearch documents are stored with their values under `fields`, so a query on `Name` searches `fields.Name`. Metadata fields such as `_id` and `_score` pass through unchanged; any other name, including one starting with `_` such as `_tags`, is a document field. A query missing a value it needs, such as `Field`, returns a failed `Result`.

## Query types

| Feature | Elasticsearch | OpenSearch | Lifti |
|---------|:------------:|:----------:|:-----:|
| **BoolQuery** (nested boolean logic) | ✅ | ✅ | ⚠️ see [boolean semantics](#boolean-semantics) |
| **TermQuery** (exact term match) | ⚠️ matches one analyzed token, ignoring ASCII case only (`Żółw` does not match `żółw`); use `Field.keyword` for the whole value | ⚠️ matches one analyzed token, ignoring ASCII case only (`Żółw` does not match `żółw`); use `Field.keyword` for the whole value | ⚠️ field ignored; accented terms don't match |
| **TermsQuery** (multiple exact terms) | ⚠️ one `terms` query: a string matches as given or lower-cased (so `Warsaw` matches the token `warsaw` but not a `WARSAW` keyword); an empty list matches nothing; more than 65,536 terms after the lower-cased copies (`index.max_terms_count`) returns a failed `Result` | ⚠️ one `terms` query: a string matches as given or lower-cased (so `Warsaw` matches the token `warsaw` but not a `WARSAW` keyword); an empty list matches nothing; more than 65,536 terms after the lower-cased copies (`index.max_terms_count`) returns a failed `Result` | ⚠️ field ignored; accented terms don't match |
| **FuzzyQuery** | ⚠️ at most 2 edits; `AUTO` when `Fuziness` is unset; the value goes through the field's analyzer and every token must match | ⚠️ at most 2 edits; `AUTO` when `Fuziness` is unset; the value goes through the field's analyzer and every token must match | ⚠️ field ignored |
| **PrefixQuery** | ⚠️ ignores ASCII case only, as TermQuery | ⚠️ ignores ASCII case only, as TermQuery | ⚠️ field ignored |
| **PrefixPhraseQuery** | ✅ | ✅ | ⚠️ field ignored |
| **LuceneQuery** (raw Lucene syntax) | ⚠️ field names need the `fields.` prefix | ⚠️ field names need the `fields.` prefix | ❌ |
| **DateRangeQuery** | ✅ | ✅ | ❌ |
| **NumericRange** | ✅ | ✅ | ❌ |
| **LongRange** | ✅ | ✅ | ❌ |
| **StringRange** | ⚠️ compares analyzed tokens; use `Field.keyword` for whole values | ⚠️ compares analyzed tokens; use `Field.keyword` for whole values | ❌ |
| **SpatialSearchQuery** | ⚠️ the field must be mapped as `geo_point`; dynamic mapping doesn't create one | ⚠️ the field must be mapped as `geo_point`; dynamic mapping doesn't create one | ❌ |

## Boolean semantics

| Feature | Elasticsearch | OpenSearch | Lifti |
|---------|:------------:|:----------:|:-----:|
| **MUST** | ✅ | ✅ | ✅ |
| **SHOULD** | ✅ Lucene semantics: alongside `MUST` or `FILTER`, only adds to the score | ✅ Lucene semantics: alongside `MUST` or `FILTER`, only adds to the score | ✅ Lucene semantics: alongside `MUST`, it changes nothing |
| **MUSTNOT** | ✅ on its own, returns the complement | ✅ on its own, returns the complement | ⚠️ a nested bool with only `MUSTNOT` clauses excludes from its parent's matches; a query whose matches would be every document except the exclusions (no positive clause, or `SHOULD` beside a `MUST` that only excludes) returns a failed `Result`, as Lifti has no query for every document |
| **FILTER** | ✅ matches without scoring | ✅ matches without scoring | ⚠️ treated as `MUST`, so it still scores |

## Search features

| Feature | Elasticsearch | OpenSearch | Lifti |
|---------|:------------:|:----------:|:-----:|
| Pagination (`Page`, `PageSize`) | ✅ `Page` counts from 1; `PageSize` defaults to 10; a page past `MaxResultWindow` (10,000 by default, matching `index.max_result_window`) returns a failed `Result` | ✅ `Page` counts from 1; `PageSize` defaults to 10; a page past `MaxResultWindow` (10,000 by default, matching `index.max_result_window`) returns a failed `Result` | ❌ ignored, all hits are returned |
| Sorting | ⚠️ sort text on `Field.keyword`; Elasticsearch rejects sorting on a text field | ⚠️ sort text on `Field.keyword`; OpenSearch rejects sorting on a text field | ❌ |
| Facets / aggregations | ❌ a query with facets returns a failed `Result` | ❌ a query with facets returns a failed `Result` | ❌ |
| Highlighting | ❌ a query with highlighting returns a failed `Result` | ❌ a query with highlighting returns a failed `Result` | ❌ |
| Post filters | ✅ | ✅ | ❌ |
| Stored fields on hits | ✅ fields as `CustomProperties`, the id unchanged in `Key`, plus `Score` and `ContentType`; `Properties` limits the fields. Values keep their JSON types: numbers written without a fraction (`int`, `long`) come back as `long`, other numbers (a `double`, even `3.0`) as `double`, dates as ISO 8601 strings, booleans as `bool` | ✅ fields as `CustomProperties`, the id unchanged in `Key`, plus `Score` and `ContentType`; `Properties` limits the fields. Values keep their JSON types: numbers written without a fraction (`int`, `long`) come back as `long`, other numbers (a `double`, even `3.0`) as `double`, dates as ISO 8601 strings, booleans as `bool` | ⚠️ hits only carry the document id, lower-cased, and `Score` |
| Total hit count | ⚠️ exact up to `MaxResultWindow` matches (10,000 by default), then a lower bound | ⚠️ exact up to `MaxResultWindow` matches (10,000 by default), then a lower bound | ✅ exact |
| Cancelling a search | ❌ `SearchAsync` takes no token | ❌ `SearchAsync` takes no token | ❌ `SearchAsync` takes no token |

The query model and fluent API can express sorting, facets, highlighting and post filters. Elasticsearch and OpenSearch apply sorting and post filters; Lifti reads none of them.

## Index operations

| Feature | Elasticsearch | OpenSearch | Lifti |
|---------|:------------:|:----------:|:-----:|
| Create index | ⚠️ a rejection is reported as success with `false` | ✅ `false` when the index already exists; any other rejection is a failed `Result` | ⚠️ silently replaces an existing index, losing its documents |
| Delete index | ⚠️ `Value` is `false` even on success | ✅ | ✅ |
| Index exists | ⚠️ transport errors read as "does not exist" | ✅ any answer other than 200 or 404, such as a 403, is a failed `Result` | ✅ |
| Ensure index exists | ⚠️ `Value` is `false` when it already exists | ✅ | ✅ |
| Index document | ✅ | ✅ | ✅ |
| Index multiple documents | ⚠️ the service indexes one by one; the provider's bulk path is unused and ignores per-item failures | ⚠️ the service indexes one by one; the provider's bulk path is unused | ⚠️ the service indexes one by one |
| Remove document | ✅ | ✅ | ✅ |
| Remove all documents | ⚠️ the service reports a count of 0 | ⚠️ the service reports a count of 0; a timed-out or partly failed delete is a failed `Result` | ⚠️ the service reports a count of 0 |
| Persistence | ✅ | ✅ | ❌ in memory only, lost on restart |

## Test coverage

Provider-agnostic bases live in `BaseTests`. Lifti runs all three (`SubQueryTranslationTestBase`, `IndexingTestBase`, `QueryResultBaseTests`). The nested and top-level boolean semantics are shared tests there, so every provider must agree on them. Elasticsearch runs all three against a real server, plus its own search tests running every query type except `SpatialSearchQuery` against it, with boolean combinations, paging, sorting, result mapping and error paths. OpenSearch runs the same three bases and the same search tests against a real OpenSearch 3.3 server, plus provider tests for bulk indexing, remove-all and configuration. `SpatialSearchQuery` is only checked to translate, as it needs an explicit `geo_point` mapping. Most of these tests assert `IsSuccess` without checking the returned value, and `FluentApi` has no tests.
