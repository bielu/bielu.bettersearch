# Road to a stable release

Where `Bielu.BetterSearch` stands and how it gets to `1.0.0`. The repository stays in changesets pre mode (`1.0.0-beta.N`) until the [exit criteria](#exit-criteria-for-10) are met. The release itself is tracked in [#168](https://github.com/bielu/bielu.bettersearch/issues/168).

## Where it stands

The shared layer is in place: provider options are validated at startup and name the failing setting ([#105](https://github.com/bielu/bielu.bettersearch/issues/105)), indexes are routed to keyed providers by name ([#100](https://github.com/bielu/bielu.bettersearch/issues/100), [#122](https://github.com/bielu/bielu.bettersearch/issues/122)), an opt-in search result cache sits over the services ([#119](https://github.com/bielu/bielu.bettersearch/issues/119)), and `GetDocumentAsync`, `CountAsync` and `UpdateDocumentAsync` exist on every interface ([#99](https://github.com/bielu/bielu.bettersearch/issues/99)). Facets ([#20](https://github.com/bielu/bielu.bettersearch/issues/20)), highlighting ([#21](https://github.com/bielu/bielu.bettersearch/issues/21)), zero-downtime rebuilds and an index name prefix ([#22](https://github.com/bielu/bielu.bettersearch/issues/22)), OpenTelemetry spans and metrics, logging and health checks ([#103](https://github.com/bielu/bielu.bettersearch/issues/103)), a client settings callback with request timeouts and retries ([#149](https://github.com/bielu/bielu.bettersearch/issues/149)), several nodes of a cluster ([#150](https://github.com/bielu/bielu.bettersearch/issues/150)), and API key, bearer and TLS settings ([#151](https://github.com/bielu/bielu.bettersearch/issues/151)) are done where the engines support them.

What follows is a summary per provider. The [feature matrix](docs/articles/feature-matrix.md) is the record, with every caveat; where the two disagree, the matrix wins and this page is out of date.

- **Elasticsearch** runs every query type, paging up to `MaxResultWindow`, sorting, post filters, term, multi-term and filter facets nested inside each other, and highlighting. Term and prefix queries match analyzed tokens, raw Lucene queries need the `fields.` prefix, and spatial queries need a field mapped as `GeoPoint` in `Mappings`. It implements get, count and update, field mappings, `WriteVisibility`, `IndexPrefix` and rebuilds through one atomic aliases request, several nodes with a `PoolType`, and basic, API key and bearer authentication with certificate fingerprint and skip-validation settings.
- **OpenSearch** does the same as Elasticsearch, except that it has no bearer authentication and its API key needs OpenSearch 3.x. The `Bielu.BetterSearch.OpenSearch.Aws` package signs requests with AWS SigV4 for Amazon OpenSearch Service and Serverless ([#152](https://github.com/bielu/bielu.bettersearch/issues/152)).
- **Meilisearch** runs every query type its filters can express. Fuzzy queries and highlighting return a failed `Result`, raw Lucene queries are parsed with Lucene.NET's `QueryParser` and translated into one filter, spatial queries need `MeilisearchOptions.GeoField`, long ranges compare as doubles, and only top-level term facets run. It implements get, count, `IndexPrefix` and update, which reads the document and stores it again whole. Rebuilds return a failed `Result`, and it takes one `ConnectionString` with an API key.
- **Typesense** runs every query type its filters can express. Fuzzy, raw Lucene and string range queries, a `MUSTNOT` range, prefix or spatial query, facets and highlighting return a failed `Result`; a page holds at most 250 hits and a sort at most 3 fields; spatial queries need `TypesenseOptions.GeoField`. Since [#146](https://github.com/bielu/bielu.bettersearch/issues/146) it implements get, count and update (which reads the document first), and since [#147](https://github.com/bielu/bielu.bettersearch/issues/147) `IndexPrefix` and rebuilds through collection aliases, under a single-writer contract; the first rebuild of a plain collection deletes it before creating the alias, so searches in between fail. It takes `AdditionalNodes`, tried in order, and an API key.
- **PostgreSQL** ([#110](https://github.com/bielu/bielu.bettersearch/issues/110)) runs every query type except raw Lucene, which returns a failed `Result`, as SQL conditions on an index stored as a table. Fuzzy queries need the `fuzzystrmatch` extension, every hit has the score 1, and facets and highlighting return a failed `Result`. It implements get, count and update, which merges the named fields in one statement, and each write commits before it returns. It has no `IndexPrefix` setting, rebuilds return a failed `Result`, and it takes a `ConnectionString` or a registered `NpgsqlDataSource`, on PostgreSQL 16 or later.
- **Lifti** (in memory) ignores `Field` on every query type, has no range, spatial or raw Lucene queries, ignores paging and sorting, has no post filters, facets or highlighting, returns only ids and scores on hits, and fails get, update and rebuilds. It is the weakest provider and the subject of [#107](https://github.com/bielu/bielu.bettersearch/issues/107).

Nothing has shipped as stable, so breaking changes are still free. The 1.0 blockers below use that window, because it closes at 1.0.

## Known defects

| Area | Behavior | Expected | Tracked in |
|---|---|---|---|
| Lifti query | `Field` is ignored: `Name=jane` also matches `City=Jane` | Only that field | [#107](https://github.com/bielu/bielu.bettersearch/issues/107) |
| Lifti index | `CreateIndexAsync` on an existing index silently replaces it, losing its documents | Success with `false`, as on the other providers | [#107](https://github.com/bielu/bielu.bettersearch/issues/107) |
| Lifti search | Searching or counting in a missing index creates it and returns no hits | A failed `Result`, as on the other providers, without creating the index | [#107](https://github.com/bielu/bielu.bettersearch/issues/107) |
| Lifti search | `Page` and `PageSize` are ignored; hits contain only an id and a score | Paged hits with fields | [#107](https://github.com/bielu/bielu.bettersearch/issues/107) |

## Blockers for 1.0

The breaking changes, in the order [#168](https://github.com/bielu/bielu.bettersearch/issues/168) gives. Items marked **stack N** are one stack of pull requests, each based on the branch of the one before it and merged from the bottom up, as [AGENTS.md](AGENTS.md#stacked-pull-requests) describes.

1. [#97](https://github.com/bielu/bielu.bettersearch/issues/97) Remove dead and placeholder public API, the Typesense public types included. **Stack 1**, based on `main`.
2. [#165](https://github.com/bielu/bielu.bettersearch/issues/165) The observer surface: removed. `IIndexingServiceAsync` and `ISearchServiceAsync` are no longer `IObservable<>` or `IDisposable`, which also ends the defect of observers seeing only their own DI scope. Code that needs a hook decorates the service in DI, as the search cache does; a singleton hook can be added after 1.0 without a break. **Stack 2**.
3. [#96](https://github.com/bielu/bielu.bettersearch/issues/96) Rename the misspelled public API (`DepedencyInjection`, `Occurance`, `PrefixLenght`, `Fuziness` and others). **Stack 3**.
4. [#163](https://github.com/bielu/bielu.bettersearch/issues/163) Query model: one source of truth for occurrence, consistent clause containers and value types. **Stack 4**.
5. [#164](https://github.com/bielu/bielu.bettersearch/issues/164) Unify the document and result shapes. **Stack 5**.
6. [#166](https://github.com/bielu/bielu.bettersearch/issues/166) Make the default interface members abstract. **Stack 6**.
7. [#94](https://github.com/bielu/bielu.bettersearch/issues/94) Full-text match query, designed before the freeze, as it touches scoring and highlighting. **Open decision.**
8. [#107](https://github.com/bielu/bielu.bettersearch/issues/107) A Lucene.NET provider, or Lifti marked as test-only and left out of the 1.0 promise; this includes the decision on the Lucene.NET prerelease dependency (NU5104). **Open decision.**
9. [#106](https://github.com/bielu/bielu.bettersearch/issues/106) Vector and hybrid search: a decision only. **Open decision.**
10. [#167](https://github.com/bielu/bielu.bettersearch/issues/167) Clear the nullable warnings, then turn on `PublicApiAnalyzers` and `CS1591`. **Stack 7**, based on the branch of the PR for #166, as it depends on the final shape of the API.

## Open decisions

These are not made yet. Until they are, nothing here or in the docs should be read as deciding them.

- **Full-text match query ([#94](https://github.com/bielu/bielu.bettersearch/issues/94)):** its shape, and whether it lands before 1.0.
- **Lifti or Lucene.NET ([#107](https://github.com/bielu/bielu.bettersearch/issues/107)):** whether a `Bielu.BetterSearch.Lucene` provider on [Lucene.NET](https://github.com/apache/lucenenet) replaces Lifti, or Lifti stays as a test-only provider outside the 1.0 promise. Lucene.NET 4.8 is still labelled beta, so a stable package depending on it gets NuGet warning NU5104. The Meilisearch provider already has that dependency, through `Lucene.Net.QueryParser` `4.8.0-beta00018`, which it uses to translate raw Lucene queries; Core and Abstractions don't.
- **Vector and hybrid search ([#106](https://github.com/bielu/bielu.bettersearch/issues/106)):** whether they are in scope for 1.0.

### Background for #107

Lifti is not archived, but its last release was v7.0.2 in December 2025, and the only activity since is one maintainer's unmerged V8 PR from May 2026.

| Option | Notes |
|---|---|
| Lucene.NET | Apache project, actively developed, release in June 2026. Elasticsearch is built on Lucene, so query semantics match. Native term, prefix, fuzzy, range, boolean, filter and raw query support, with facet, highlighter and spatial packages. `RAMDirectory` keeps an index in memory, and `FSDirectory` on disk. Still labelled beta |
| Lifti | Slowing, and has no ranges, filters or geo |
| LunrCore | Builds an immutable index, so it can't take incremental updates |
| SQLite FTS5 | No fuzzy search, and needs its own query translation |
| Writing our own | Far more work than this project needs to own |

A spike on .NET 10 with Lucene.NET `4.8.0-beta00018` ran term, prefix, fuzzy, numeric range, string range, `MUST` + `MUST_NOT`, `MUST` + `SHOULD`, filter context, raw query syntax and delete-then-search in memory, all with the expected results. The facet, highlighter and spatial packages were restored but not exercised.

## Deferred until after 1.0

These are additive, so they can ship in a minor release:

- [#98](https://github.com/bielu/bielu.bettersearch/issues/98) Typed documents: map POCOs to and from `SearchDocument`.
- [#101](https://github.com/bielu/bielu.bettersearch/issues/101) Page past the result window with cursor-based paging.
- [#161](https://github.com/bielu/bielu.bettersearch/issues/161) Client factories keep every superseded client after a credential change.
- [#29](https://github.com/bielu/bielu.bettersearch/issues/29) Extract shared provider logic.
- [#28](https://github.com/bielu/bielu.bettersearch/issues/28) An Algolia provider.
- [#108](https://github.com/bielu/bielu.bettersearch/issues/108) An Azure AI Search provider.

## Exit criteria for 1.0

Each of these can be checked:

- Every ⚠️ and ❌ in the [feature matrix](docs/articles/feature-matrix.md) is pinned by a shared `BaseTests` test or a provider test, and every ✅ by a shared test.
- No `NotImplementedException` on a public path.
- No compiler or analyzer warnings in shipping projects.
- The public API is tracked by `PublicApiAnalyzers` and documented (`CS1591` on).
- 70%+ line coverage on library code.
- No stable package depends on a prerelease package, or the exception is written down here.
- Every engine's integration tests pass against the AppHost images.

## Release steps

1. Put the blockers on a `1.0` milestone.
2. Merge the blockers, then check the exit criteria.
3. Publish a release candidate while still in pre mode: `npx changeset pre exit`, then `npx changeset pre enter rc` in the same pull request, so the Version Packages PR publishes `1.0.0-rc.N`. A `pre exit` on its own makes the next Version Packages PR clear `VersionSuffix` and publish 1.0 stable.
4. At least one real consumer runs the release candidate.
5. Release 1.0: `npx changeset pre exit`, and merge the Version Packages PR it produces.
