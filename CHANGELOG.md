# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). The `[Unreleased]`
section and released version entries are managed with
[changesets](https://github.com/changesets/changesets); see [`.changeset/README.md`](.changeset/README.md).

## [Unreleased]
## [1.0.0-beta.3] - 2026-11-07

### Minor Changes

- [#16](https://github.com/bielu/bielu.bettersearch/pull/16) [`0d8fef6`](https://github.com/bielu/bielu.bettersearch/commit/0d8fef63996fc44afb99b9df51c049ddc1dc1832) Thanks [@tessl-app](https://github.com/apps/tessl-app)! - Implement search in the Elasticsearch provider. `SearchAsync` translates every sub-query type (bool, term, terms, prefix, prefix phrase, fuzzy, numeric, long, date and string ranges, raw Lucene, geo distance), applies `Page`, `PageSize`, `SortOrder`, `Properties` and post filters, and maps hits with their id, score, type and fields. Server errors and unsupported facets and highlighting return a failed `Result`. The translator is now `IQueryTranslateServiceAsync<Query>` with one `ISubQueryTranslator<Query>` per sub-query type, `ISearchModel` gains `Score`, and `SearchServiceAsync` returns a provider's failed `Result` instead of throwing.

  `BoolSearchSubQuery.Add` now sets the clause's occurrence instead of dropping it, and Lifti combines clauses with the same Lucene semantics as Elasticsearch: `SHOULD` next to a `MUST` no longer widens the result, `FILTER` acts as `MUST`, and `MUSTNOT` with no positive clause anywhere in the query returns a failed `Result`, while a nested bool with only `MUSTNOT` clauses excludes from its parent's matches and, as a `MUST`, makes its parent's `SHOULD` clauses optional. Lifti hits now carry `Score`. In Elasticsearch, `TermsQuery` is one `terms` query, post filters are translated through the new `IPostFilterTranslateServiceAsync<T>`, the client factory is a singleton that reuses one client per connection, a page past `ElasticSearchOptions.MaxResultWindow` returns a failed `Result`, the total hit count is exact up to `MaxResultWindow`, a `terms` query past `index.max_terms_count` returns a failed `Result`, hit fields keep their JSON types, and only known metadata fields such as `_id` skip the `fields.` prefix.

  Breaking: `ISearchModel` gains `Score`, so an implementation outside this library must add it, and `ElasticSearchSearchProviderAsync` takes `IPostFilterTranslateServiceAsync<Query>` and `IOptionsMonitor<ElasticSearchOptions>` in its constructor.

### Patch Changes

- [#8](https://github.com/bielu/bielu.bettersearch/pull/8) [`265e740`](https://github.com/bielu/bielu.bettersearch/commit/265e740ff728ccd0325403a1518ce8c71778ecfa) Thanks [@bielu](https://github.com/bielu)! - Fix the Elasticsearch provider's `IndexDocumentAsync`, which threw because the request named no index.

- [#5](https://github.com/bielu/bielu.bettersearch/pull/5) [`9085cd8`](https://github.com/bielu/bielu.bettersearch/commit/9085cd8ac07901fb9c4ae20ff06ac2a5cebc0b03) Thanks [@bielu](https://github.com/bielu)! - Target .NET 10, update dependencies, and adopt the bielu ecosystem repository standards. Packages now carry license, repository and Source Link metadata, symbols and a readme, and are versioned through changesets.

- [#25](https://github.com/bielu/bielu.bettersearch/pull/25) [`a5a747e`](https://github.com/bielu/bielu.bettersearch/commit/a5a747e4c4e293d299df939d2cb0f9bfa7846e24) Thanks [@bielu](https://github.com/bielu)! - Fix Lifti dropping the matches of a double negation. A `MUSTNOT` of a nested bool with only `MUSTNOT` clauses now requires those clauses' matches: on its own it returns them (it returned nothing), and beside `SHOULD` clauses it returns the documents matching both (the nested matches were ignored), as in Lucene and Elasticsearch. A `SHOULD` of a nested bool with only `MUSTNOT` clauses no longer fails when the query can still be answered, for example beside a double negation or another `SHOULD` under a positive parent.

- [#26](https://github.com/bielu/bielu.bettersearch/pull/26) [`4457f8f`](https://github.com/bielu/bielu.bettersearch/commit/4457f8f2bd5a3c791e9f8e0a157469f6fec42f12) Thanks [@bielu](https://github.com/bielu)! - Every package now ships with an icon.

- [#27](https://github.com/bielu/bielu.bettersearch/pull/27) [`e711fae`](https://github.com/bielu/bielu.bettersearch/commit/e711faef93eb24c1b633bde467d9ca407721b689) Thanks [@bielu](https://github.com/bielu)! - The README, which ships in every package, now follows the layout of the other Bielu packages: badges, logo, quick start, features, package and provider tables, and links to the documentation site.

- [#6](https://github.com/bielu/bielu.bettersearch/pull/6) [`0f60c83`](https://github.com/bielu/bielu.bettersearch/commit/0f60c83f230f03e6d8fd29724a694648626d7d7a) Thanks [@bielu](https://github.com/bielu)! - Adopt `Bielu.StaticCode.Analyzers`. Renames `IQueryTranslateServiceAsync.TranslateMainQuery` to `TranslateMainQueryAsync` and the Core DI class `RegisterSearchExtension` to `BetterSearchServiceCollectionExtensions`, and adds `ConfigureAwait(false)` throughout library code.

