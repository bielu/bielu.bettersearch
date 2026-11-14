---
"bielu-bettersearch": patch
---

Make the sub-query defaults agree (#92). Breaking: each changed default is listed here.

- `Occurance` now defaults to `MUST` on every sub-query: `TermSubQuery`, `TermsSubQuery`, `FuzzyQuery`, `PrefixSubQuery`, `PrefixPhraseSubQuery`, `LuceneSubQuery`, `SpatialSearchQuery` and the ranges (`NumericRange`, `LongRange`, `DateRangeQuery`, `StringRange`). It was `0`, which is not a defined value. A sub-query put in `BoolSearchSubQuery.NestedQueries` directly was already treated as `MUST` by every provider, so its matches do not change.
- `BoostValue` now defaults to `1` on `LuceneSubQuery` and `SpatialSearchQuery`, as on every other sub-query. It was `0`, which the providers read as unset, so the query sent does not change.
- `IncludeMinEdge` and `IncludeMaxEdge` now default to `true` on every range, so a range is inclusive at both ends unless an edge is set to `false`. They were `false`, so a range built without setting them now also matches values equal to its bounds. The fluent `WithMinValue` and `WithMaxValue` of every range configurator now default `includeEdge` to `true` too.
- A `NumericRange`, `LongRange`, `DateRangeQuery` or `StringRange` with neither `MinValue` nor `MaxValue` now returns a failed `Result` on every provider, with the message `<type> has neither MinValue nor MaxValue; set at least one bound.` On Elasticsearch, OpenSearch and Meilisearch it used to match every document that has the field; on Lifti it failed saying ranges are not supported, which it still says for a range with a bound.
