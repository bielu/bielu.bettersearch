# BetterSearch Provider Feature Matrix

What each provider really does today. This is a beta: see [ROADMAP.md](./ROADMAP.md) for the plan.

✅ works · ⚠️ works with the caveat noted · ❌ not available

**The Elasticsearch provider cannot search yet.** `SearchAsync` throws `NotImplementedException`, so every query row below is ❌ for it. Its query translator is a stub.

## Query types

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| **BoolQuery** (nested boolean logic) | ❌ | ⚠️ see [boolean semantics](#boolean-semantics) |
| **TermQuery** (exact term match) | ❌ | ⚠️ field ignored; accented terms don't match |
| **TermsQuery** (multiple exact terms) | ❌ | ⚠️ field ignored; accented terms don't match |
| **FuzzyQuery** | ❌ | ⚠️ field ignored |
| **PrefixQuery** | ❌ | ⚠️ field ignored |
| **PrefixPhraseQuery** | ❌ | ⚠️ field ignored |
| **LuceneQuery** (raw Lucene syntax) | ❌ | ❌ |
| **DateRangeQuery** | ❌ | ❌ |
| **NumericRange** | ❌ | ❌ |
| **LongRange** | ❌ | ❌ |
| **StringRange** | ❌ | ❌ |
| **SpatialSearchQuery** | ❌ | ❌ |

## Boolean semantics

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| **MUST** | ❌ | ✅ |
| **SHOULD** | ❌ | ⚠️ `MUST` + `SHOULD` returns the union, where Lucene semantics require the `MUST` |
| **MUSTNOT** | ❌ | ⚠️ inverted when it is the first or only clause: returns the matches |
| **FILTER** | ❌ | ⚠️ unsupported, but treated as a match when it is the first or only clause |

## Search features

| Feature | Elasticsearch | Lifti |
|---------|:------------:|:-----:|
| Pagination (`Page`, `PageSize`) | ❌ | ❌ ignored, all hits are returned |
| Sorting | ❌ | ❌ |
| Facets / aggregations | ❌ | ❌ |
| Highlighting | ❌ | ❌ |
| Post filters | ❌ | ❌ |
| Stored fields on hits | ❌ | ❌ hits only carry the document id, lower-cased |
| Cancelling a search | ❌ | ❌ `SearchAsync` takes no token |

The query model and fluent API can express sorting, facets, highlighting and post filters, but no provider reads them.

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

Provider-agnostic bases live in `BaseTests`. Lifti runs all three (`SubQueryTranslationTestBase`, `IndexingTestBase`, `QueryResultBaseTests`). Elasticsearch runs only `IndexingTestBase` and the dependency injection checks. Most of these tests assert `IsSuccess` without checking the returned value, and `FluentApi` has no tests.
