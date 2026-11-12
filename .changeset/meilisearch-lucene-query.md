---
"bielu-bettersearch": minor
---

The Meilisearch provider now translates `LuceneSubQuery` instead of failing it (#34). The string is parsed with Lucene.NET's `QueryParser` (`Lucene.Net.QueryParser` 4.8.0-beta00018, a prerelease, referenced by `Bielu.BetterSearch.Meilisearch` only) and turned into one filter: field terms, a trailing `*` prefix, ranges (numbers and ISO 8601 dates also compare as such), `_exists_:field`, `field:*` and `AND`/`OR`/`NOT`/`+`/`-` translate exactly. A fuzzy term and a phrase translate approximately, and the result carries the new public `Bielu.BetterSearch.Abstractions.Query.TranslationWarning` success reason, which `SearchServiceAsync` returns with the hits. A term without a field, proximity, boosts, regular expressions and other wildcards still return a failed `Result` that names the clause.
