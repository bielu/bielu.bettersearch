# Road to a stable release

Where `Bielu.BetterSearch` stands and how it gets to `1.0.0`. The repository stays in changesets pre mode (`1.0.0-beta.N`) until the exit criteria at the end are met.

## Where it stands

The repository tooling now matches the bielu ecosystem: .NET 10, current packages, shared CI and releases, analyzers, Aspire. The code itself is an early beta:

- **The Elasticsearch provider searches** with every query type, paging, sorting and post filters, but has no facets or highlighting, and its index operations have the defects listed below.
- **The OpenSearch provider searches** the same way as Elasticsearch, with every index operation, but likewise has no facets or highlighting.
- **The Meilisearch provider searches** with every query type its filters can express, paging, sorting and post filters; spatial queries need `MeilisearchOptions.GeoField`; fuzzy and raw Lucene queries return a failed `Result`, and it has no facets or highlighting.
- **The in-memory provider gives wrong answers** for some queries (see below), and it is built on Lifti, which should be replaced.
- **Half the query model does little.** Pagination, sorting, facets, highlighting and post filters can be expressed; only Elasticsearch, OpenSearch and Meilisearch read pagination, sorting and post filters.
- **The tests pass while all of this is true.** They mostly assert `IsSuccess`, and `Result<bool>` can succeed with `false`.

Nothing has shipped as stable, so breaking changes are still free. That window should be used, because it closes at 1.0.

## Confirmed defects

Each of these was reproduced by running the code against the Lifti provider, except where marked *by inspection*.

| Area | Behavior | Expected |
|---|---|---|
| Lifti query | `Field` is ignored: `Name=jane` also matches `City=Jane` | Only that field |
| Lifti query | Accented terms never match: `Żółw` finds nothing, because terms are upper-cased by hand instead of using the index tokenizer | A match |
| Lifti index | `CreateIndexAsync` on an existing index silently wipes its documents | An error or a no-op |
| Core | `RemoveAllDocumentsAsync` reports 0 removed, whatever the real count | The count |
| Core | `IndexMultipleDocumentsAsync` indexes one at a time, so the provider bulk path is never used (*by inspection*) | Bulk indexing |
| Core | `SearchApplicationConfiguration.CurrentInstance` is a process-wide mutable singleton shared by every `AddBetterSearch` call (*by inspection*) | Per-container configuration |
| Core | Services are registered twice, once by the provider and once by `AddBetterSearch` | Once |
| Core | Indexing observers live on a scoped service, so a subscription only sees the operations of its own scope (*by inspection*) | See phase 1 |
| Models | `new TermSubQuery().Occurance` is `0`, which is not a defined enum value | A defined default |
| Lifti search | `Page` and `PageSize` are ignored; hits contain only an id | Paged hits with fields |
| Fluent API | `.Should()` on a sub-query configurator is silently lost, and `.Must()`, `.MustNot()` and `.Filter()` take the same path | The chosen occurrence |
| Elasticsearch | Provider methods return `Result.Ok()` where a `Result<bool>` or `Result<int>` is due, so `Value` is `false` or `0` even on success (*by inspection*) | The real value |
| Elasticsearch | `CreateIndexAsync` reports a server rejection as a successful `false`; `IndexExistsAsync` reads transport errors as "does not exist" (*by inspection*) | A failed `Result` |
| Elasticsearch | Bulk indexing ignores per-item failures (*by inspection*) | A failed `Result` naming them |
| Elasticsearch | Merging index settings mutates the live options object and only merges the connection string, not credentials (*by inspection*) | A merged copy |

## Replacing Lifti: use Lucene.NET

Lifti is not archived, but its last release was v7.0.2 in December 2025, and the only activity since is one maintainer's unmerged V8 PR from May 2026. That is too thin to build a default provider on.

**Recommendation: a new `Bielu.BetterSearch.Lucene` provider on [Lucene.NET](https://github.com/apache/lucenenet), using `RAMDirectory` for in-memory.**

| Option | Verdict | Why |
|---|---|---|
| **Lucene.NET** | **Use** | Apache project, actively developed, release in June 2026. Elasticsearch is built on Lucene, so query semantics match and the two providers can share one conformance suite. Native term, prefix, fuzzy, range, boolean, filter and raw query support, with facet, highlighter and spatial packages. Swapping to `FSDirectory` adds persistence |
| Lifti | Replace | Slowing, and has no ranges, filters or geo |
| LunrCore | No | Builds an immutable index, so it can't take incremental updates |
| SQLite FTS5 | No | No fuzzy search, and needs its own query translation |
| Writing our own | No | Far more work than this project needs to own |

A spike on .NET 10 with `4.8.0-beta00018` ran term, prefix, fuzzy, numeric range, string range, `MUST` + `MUST_NOT`, `MUST` + `SHOULD`, filter context, raw query syntax and delete-then-search in memory, all with the expected results. The facet, highlighter and spatial packages were restored but not exercised.

**The catch:** Lucene.NET 4.8 is still labelled beta, so a stable `Bielu.BetterSearch.Lucene` would depend on a prerelease (NuGet warning NU5104). `Examine.Lucene` lives with the same situation. The options are to accept it for that one package, or to keep that package on a prerelease version until Lucene.NET ships stable. Core and Abstractions are unaffected either way.

## Phases

Each item is about one PR. The order matters: the API comes first because every provider is written against it.

### Phase 1: fix the contract

Breaking, and cheapest now.

- Tests that assert values: make the base tests fail on the defects above, with an isolated index per test.
- Correct the names: `DepedencyInjection`, `Occurance`, `PrefixLenght`, `Fuziness`, `MultiTermAggreation`, `CreateGoeDistanceQuery`, `DepedenyInjectionTestBase`.
- Reshape the model: merge `Bielu.BetterSearch.Models` into Abstractions. Return typed field values on hits from explicit mappings; Elasticsearch hits keep their JSON types until then. Rename `DeleteDocumentRequest.Type`, which actually holds the index name, to `IndexName` to match `DeleteAllDocumentsRequest.IndexName`; renaming it after the stable release would break the public API. (Done in #17: the unused generics are gone, `IEntity<T>` has its `Id`, `Score` is on the hit, ids come back unchanged, the CMS leftovers are dropped and `FilterByCulture` filters on a plain culture field.)
- Resolve the nullable warnings by making the model honest about what is required.
- Stop catching `OperationCanceledException` in the indexing providers, and keep exceptions through `ExceptionalError`. (Done in #17: `SearchAsync` and the validator take a `CancellationToken`, and a cancelled search throws.)
- Remove the global configuration singleton and the duplicate registrations. Add the missing `SetDocumentValidatorType`.
- Replace the `IObservable` surface with something that fits its use, such as an indexing pipeline or events, or remove it. (The `IObserver` contract is fixed in #56: `OnNext` after each successful operation, nothing for a rejection or failure, `OnCompleted` only when the service is disposed.)
- Pass bulk through to the provider from `IndexMultipleDocumentsAsync`.
- Deduplicate the fluent API and fix the lost occurrence, with its first tests.
- Decide the `SHOULD` semantics: follow Lucene, and write them down.

### Phase 2: Lucene.NET provider

- Implement `Bielu.BetterSearch.Lucene` against the phase 1 contract: every query type, pagination, stored fields and sorting.
- Remove `Bielu.BetterSearch.Lifti`.
- Accent and case behavior comes from a configurable analyzer.

### Phase 3: Elasticsearch search

- ~~Implement sub-query translators, `SearchAsync`, result mapping, pagination and sorting.~~ Done; facets and highlighting are still to come.
- Run the same conformance suite as Lucene.
- Fix the provider defects above: real return values, per-item bulk errors, non-mutating settings, options validation at startup.
- Add a sample API to the AppHost, with `ServiceDefaults`.

### Phase 4: stabilize

- One conformance suite covering every query type and boolean combination, run against both providers. the feature matrix (`docs/articles/feature-matrix.md`) becomes a record of its results.
- OpenTelemetry traces and metrics, as in `Bielu.PersistentQueues`.
- Facets and highlighting, or remove them from the query model for 1.0.
- Turn on `Microsoft.CodeAnalysis.PublicApiAnalyzers`, enable CS1591 and document the public API.
- Add `ship-public-api.mjs` to the release flow, a package icon, and an entry on packages.bielu.pl.
- `npx changeset pre exit` for a release candidate.

## Recommended cuts for 1.0

- **`Bielu.BetterSearch.EndpointApi`:** delete it now. Its controllers are empty classes and it doesn't reference ASP.NET, so it can return as a minimal API package after 1.0.
- **Spatial and raw Lucene queries:** keep them if Lucene.NET makes them cheap, but don't let them hold up the release.

## Exit criteria for 1.0

- Both providers pass one conformance suite, and the feature matrix (`docs/articles/feature-matrix.md`) has no ⚠️.
- No `NotImplementedException` on a public path, and no compiler or analyzer warnings.
- Public API tracked and documented; 70%+ line coverage on library code.
- At least one real consumer has run a release candidate.
