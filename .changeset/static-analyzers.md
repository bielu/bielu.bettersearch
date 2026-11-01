---
"bielu-bettersearch": patch
---

Adopt `Bielu.StaticCode.Analyzers`. Renames `IQueryTranslateServiceAsync.TranslateMainQuery` to `TranslateMainQueryAsync` and the Core DI class `RegisterSearchExtension` to `BetterSearchServiceCollectionExtensions`, and adds `ConfigureAwait(false)` throughout library code.
