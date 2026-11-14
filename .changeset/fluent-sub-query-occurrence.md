---
"bielu-bettersearch": patch
---

Fix the fluent API losing the occurrence chosen on a sub-query. `Must()`, `Should()`, `MustNot()` and `Filter()` on a term, terms, fuzzy, prefix, prefix phrase, spatial or range configurator now decide how that clause is added; without one, the clause takes the parent's occurrence as before. The range configurators gain these four methods. The chosen occurrence is also written to the configurator's `Query`, so a query read straight from `Query` carries it too. A nested bool created with `CreateBoolQuery` now defaults its clauses to `MUST` instead of the undefined value `0`, and a top-level bool's `Occurance` matches the occurrence it is added under. Breaking: the sub-query configurators share a new `SubQueryConfigurator<TSelf>` base, and the `Occurance` property of `TermQueryConfigurator`, `TermsQueryConfigurator` and `SpatialQueryConfigurator` is now `Occurance?`, where `null` means the parent's.
