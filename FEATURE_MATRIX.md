# BetterSearch Provider Feature Matrix

What each provider really does today. This is a beta: see [ROADMAP.md](./ROADMAP.md) for the plan.

✅ works · ⚠️ works with the caveat noted · ❌ not available

Elasticsearch documents are stored with their values under `fields`, so a query on `Name` searches `fields.Name`. Elasticsearch metadata fields such as `_id` and `_score` pass through unchanged; any other name, including one starting with `_` such as `_tags`, is a document field. A query missing a value it needs, such as `Field`, returns a failed `Result`.

## Query types

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| **BoolQuery** (nested boolean logic) | ✅ | ⚠️ see [boolean semantics](#boolean-semantics) |
| **TermQuery** (exact term match) | ⚠️ matches one analyzed token, ignoring ASCII case only (`Żółw` does not match `żółw`); use `Field.keyword` for the whole value | ⚠️ field ignored; accented terms don't match |
| **TermsQuery** (multiple exact terms) | ⚠️ one `terms` query: a string matches as given or lower-cased (so `Warsaw` matches the token `warsaw` but not a `WARSAW` keyword); an empty list matches nothing | ⚠️ field ignored; accented terms don't match |
| **FuzzyQuery** | ⚠️ at most 2 edits; `AUTO` when `Fuziness` is unset; the value goes through the field's analyzer and every token must match | ⚠️ field ignored |
| **PrefixQuery** | ⚠️ ignores ASCII case only, as TermQuery | ⚠️ field ignored |
| **PrefixPhraseQuery** | ✅ | ⚠️ field ignored |
| **LuceneQuery** (raw Lucene syntax) | ⚠️ field names need the `fields.` prefix | ❌ |
| **DateRangeQuery** | ✅ | ❌ |
| **NumericRange** | ✅ | ❌ |
| **LongRange** | ✅ | ❌ |
| **StringRange** | ⚠️ compares analyzed tokens; use `Field.keyword` for whole values | ❌ |
| **SpatialSearchQuery** | ⚠️ the field must be mapped as `geo_point`; dynamic mapping doesn't create one | ❌ |

## Boolean semantics

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| **MUST** | ✅ | ✅ |
| **SHOULD** | ✅ Lucene semantics: alongside `MUST` or `FILTER`, only adds to the score | ✅ Lucene semantics: alongside `MUST`, it changes nothing |
| **MUSTNOT** | ✅ on its own, returns the complement | ⚠️ on its own, returns a failed `Result`, as Lifti has no query for every document |
| **FILTER** | ✅ matches without scoring | ⚠️ treated as `MUST`, so it still scores |

## Search features

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| Pagination (`Page`, `PageSize`) | ✅ `Page` counts from 1; `PageSize` defaults to 10; a page past `MaxResultWindow` (10,000 by default, matching `index.max_result_window`) returns a failed `Result` | ❌ ignored, all hits are returned |
| Sorting | ⚠️ sort text on `Field.keyword`; Elasticsearch rejects sorting on a text field | ❌ |
| Facets / aggregations | ❌ a query with facets returns a failed `Result` | ❌ |
| Highlighting | ❌ a query with highlighting returns a failed `Result` | ❌ |
| Post filters | ✅ | ❌ |
| Stored fields on hits | ✅ fields as `CustomProperties`, the id unchanged in `Key`, plus `Score` and `ContentType`; `Properties` limits the fields | ⚠️ hits only carry the document id, lower-cased, and `Score` |
| Total hit count | ⚠️ exact up to 10,000 matches, then a lower bound | ✅ exact |
| Cancelling a search | ❌ `SearchAsync` takes no token | ❌ `SearchAsync` takes no token |

The query model and fluent API can express sorting, facets, highlighting and post filters. Elasticsearch applies sorting and post filters; Lifti reads none of them.

## Index operations

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| Create index | ⚠️ a rejection is reported as success with `false` | ⚠️ silently replaces an existing index, losing its documents |
| Delete index | ⚠️ `Value` is `false` even on success | ✅ |
| Index exists | ⚠️ transport errors read as "does not exist" | ✅ |
| Ensure index exists | ⚠️ `Value` is `false` when it already exists | ✅ |
| Index document | ✅ | ✅ |
| Index multiple documents | ⚠️ the service indexes one by one; the provider's bulk path is unused and ignores per-item failures | ⚠️ the service indexes one by one |
| Remove document | ✅ | ✅ |
| Remove all documents | ⚠️ the service reports a count of 0 | ⚠️ the service reports a count of 0 |
| Persistence | ✅ | ❌ in memory only, lost on restart |

## Test coverage

Provider-agnostic bases live in `BaseTests`. Lifti runs all three (`SubQueryTranslationTestBase`, `IndexingTestBase`, `QueryResultBaseTests`). The nested and top-level boolean semantics are shared tests there, so both providers must agree on them. Elasticsearch runs all three against a real server, plus its own search tests running every query type except `SpatialSearchQuery` against it, with boolean combinations, paging, sorting, result mapping and error paths. `SpatialSearchQuery` is only checked to translate, as it needs an explicit `geo_point` mapping. Most of these tests assert `IsSuccess` without checking the returned value, and `FluentApi` has no tests.
