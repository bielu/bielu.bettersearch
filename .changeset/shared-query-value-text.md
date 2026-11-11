---
"bielu-bettersearch": patch
---

The query value text helper is compiled into each provider instead of shared from `Bielu.BetterSearch.Abstractions` through `InternalsVisibleTo` (#67), so a provider no longer calls into another package's internals and can't fail with `MissingMethodException` when paired with a newer Abstractions package. Meilisearch now formats numbers in filters with the same helper, only dropping the `+` from a positive exponent (`1E20`), which Meilisearch filters need; filter text for finite numbers is unchanged. A Meilisearch `TermSubQuery` or `TermsSubQuery` value of `NaN` or an infinity now returns a failed `Result`, where before the filter read it as text and matched no number.
