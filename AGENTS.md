# AGENTS.md

Guidance for AI coding agents working in this repository. Human contributors should also read [CONTRIBUTING.md](./CONTRIBUTING.md).

## What this project is

`Bielu.BetterSearch` is a **provider-agnostic search abstraction** for .NET: a query model and service interfaces, with engine-specific providers behind them. It is in beta. [ROADMAP.md](./ROADMAP.md) is the plan for reaching a stable release, and [docs/articles/feature-matrix.md](./docs/articles/feature-matrix.md) records what each provider really supports; keep both accurate.

## Repository layout

The solution is `src/Bielu.BetterSearch.slnx`. Everything, tests included, lives under `src/`.

| Project | Purpose |
|---------|---------|
| `Bielu.BetterSearch.Abstractions` | Query model (`SearchQuery`, sub-queries) and service interfaces |
| `Bielu.BetterSearch.Models` | `SearchDocument` and request models |
| `Bielu.BetterSearch.Core` | `AddBetterSearch`, `IndexingServiceAsync`, `SearchServiceAsync` |
| `Bielu.BetterSearch.FluentApi` | Fluent query builder |
| `Bielu.BetterSearch.ElasticSearch` | Elasticsearch provider |
| `Bielu.BetterSearch.Meilisearch` | Meilisearch provider |
| `Bielu.BetterSearch.OpenSearch` | OpenSearch provider |
| `Bielu.BetterSearch.Typesense` | Typesense provider |
| `Bielu.BetterSearch.Lifti` | In-memory provider |
| `Bielu.BetterSearch.EndpointApi` | Placeholder (not packed) |
| `examples/Bielu.BetterSearch.Examples.AppHost` | Aspire AppHost running the search engines and their UIs, for local development and the integration tests (not packed) |
| `Bielu.BetterSearch.BaseTests` | Provider-agnostic test bases, inherited by each provider's test project (not packed) |
| `Bielu.BetterSearch.*.Tests` | Test projects (not packed) |

## Conventions

- **Target framework:** `net10.0`, set in `src/Directory.Build.props`. Requires the **.NET 10 SDK**.
- **Language settings:** `Nullable` and `ImplicitUsings` enabled. Respect nullable annotations.
- **Central package management:** all package versions live in [`Directory.Packages.props`](./Directory.Packages.props). Reference packages with `<PackageReference Include="..." />` and **no `Version` attribute**.
- **Shared MSBuild properties:** [`src/Directory.Build.props`](./src/Directory.Build.props). **Version** is centralized in [`version.props`](./version.props) and written by the release tooling. Never edit it or hardcode versions in csproj files.
- **Providers** implement `IIndexingProviderAsync` and `ISearchProviderAsync` and register through a `Configurator` extension. A new engine is a new `Bielu.BetterSearch.<Name>` project, not a change to Core. Query behavior must match across providers; where an engine can't support something, return a failed `Result` and record it in the feature matrix.
- **Errors** flow through `FluentResults`: return a failed `Result` rather than throwing for anything a caller can act on.
- **Formatting:** governed by [`.editorconfig`](./.editorconfig) (4-space indent for C#, final newline). File-scoped namespaces.

## Engineering principles

- **SOLID**: small focused interfaces, dependencies consumed through DI, engines added as new providers rather than edits to Core.
- **DRY**: put provider-agnostic behavior tests in `BaseTests` so every provider inherits them.
- **KISS**: prefer the simplest solution that fits; no speculative generality.
- **Proper abstraction**: mark implementation details `internal`; a published NuGet version is immutable.

## Code coverage

Target **70%+ line coverage** for code you add or change. Coverage comes from `coverlet.MTP`, referenced by every test project; CI passes `--coverlet` and posts the report on the PR. `BaseTests` is excluded through `[assembly: ExcludeFromCodeCoverage]`.

## Build, test, format

Tests run on Microsoft Testing Platform, selected in [`global.json`](./global.json), so `dotnet test` takes MTP arguments:

```bash
dotnet build src/Bielu.BetterSearch.slnx
dotnet test --solution src/Bielu.BetterSearch.slnx
dotnet test --project src/Bielu.BetterSearch.Lifti.Tests --coverlet --coverlet-output-format cobertura
dotnet format --verify-no-changes src/Bielu.BetterSearch.slnx
```

## Testing conventions

- **xUnit** (`xunit.v3`) + **Shouldly** assertions.
- Assert on returned values, not only on `IsSuccess`: `Result<bool>` can succeed with `false`.
- Provider tests that need a real engine start it through the Aspire AppHost (`ElasticsearchResource`), once per test run, so they need Docker. Don't add Testcontainers.
- Name tests `Method_Scenario_ExpectedResult`.
- Add or update tests for any behavior change, covering the happy path and the error paths.

## Common review findings

These are the issues reviews raise most often. Check each one before you push, rather than waiting for review rounds to find them.

### Work through the logic before you push

About a third of review findings are logic bugs that build, pass the tests and read fine. Rules alone won't catch them, so before you push, run each scenario below that applies against your change and write down what happens. Each one comes from a real bug in this repository.

- **Query semantics follow Lucene/Elasticsearch.** The feature matrix sets Lucene bool semantics as the reference, so write out the expected matches for each new or changed query path and compare. Cover `SHOULD` beside `MUST`, a nested bool with only `MUSTNOT` clauses, a double negation, and a nested bool whose occurrence is set through `Add`. Put the case in a shared `BaseTests` test, with data where the right and the wrong semantics return different documents.
- **Multi-valued and analyzed fields.** A field holds a list, and a text field is split into tokens. A range written as `x >= min AND x <= max` can match one value on each bound, with no single value inside the range. A fuzzy query on `3.5` can match `35`. A term query for a value the analyzer splits matches nothing. Check what the engine actually stores for the value: its type, case, tokens and format.
- **Success with zero hits is a bug when the query can never match.** Translating to a field or prefix that can't hold the value (a date on `fields.`, a string term on a numeric field, a user field treated as metadata because it starts with `_`) returns `Ok` with nothing in it. Fail with a clear message, or make it match.
- **Types survive the round trip.** Index a `long` above 2^53, a `3.0` double, a `DateTime` with every `Kind`, a `bool`, a very small and a very large `double`, and read them back and query them. A `cond ? long : double` ternary converts to `double` silently. Invariant culture is necessary but not enough: `DateTime` and `bool` must also match the engine's own format.
- **Two callers at once.** For each check-then-act (exists → create, get → create, read settings → write settings), ask what happens when another caller, process or dashboard acts in between. Does the loser get a failed or a `false` result for something that worked? Can it recreate an index that someone just deleted? A shared object filled in field by field can be read half-filled by another thread.
- **Callbacks re-enter.** An observer can throw, unsubscribe, or dispose the service from inside `OnNext` or `OnCompleted`. Iterate over a snapshot, never send `OnNext` after `OnCompleted`, and don't let a throwing observer turn a committed write into a failure or skip the observers after it.
- **Partial failure.** Item 3 of 5 fails, or the engine answers 200 with `timed_out`, shard failures, or a partial delete. Say exactly what the caller gets back and what is already stored, and make the docs say the same.
- **State changes over time.** A cache, a static field, or a check that was trusted for N seconds: what happens when the index is deleted or recreated outside this process, when two app versions run side by side, when options change through `IOptionsMonitor`, or when settings change after documents were indexed? For Lifti, also ask what happens after a restart, when every index is empty.
- **Defaults and inheritance.** If a default enum value (`NONE`, `0`) means "inherit", an index can't opt out. Check that a merge copies every inherited setting, including credentials. Validate config up front (empty secrets, a URI with no scheme, a page size of 0) and fail with a message that names the setting.
- **The DI graph.** A provider must register only its own services, because `AddBetterSearch` registers the shared ones. Take dependencies through the registered interface, not the concrete class, so a replacement or decorator applies to the whole request.
- **Verify engine behavior; don't recall it.** Claims about what Elasticsearch, OpenSearch or Meilisearch does (case handling, coercion, date formats, auth schemes, limits) have been wrong in both PRs and reviews. Check them against the engine running in the AppHost, and pin them with an integration test. Also keep each AppHost image within the server versions its client package supports (see the comments in `AppHost.cs`). A matching major version isn't required.
- **Cost at realistic sizes.** Think about 100k documents, 10k terms values, a page past 10,000, and a single-document write in a hot loop. Look for requests that grow without bound, one clause per value, an extra round trip on every write, and materializing all hits when one page was asked for.

When you find a scenario that breaks, write the failing test first and then fix the code.

### Change every provider, and both sides of the round trip

- When you fix a behavior in one provider, search the other three for the same code and fix it there too, or say in the PR why not. Elasticsearch and OpenSearch are near-copies, and a fix applied to only one of them is the most common miss.
- Check the **index-time and query-time** paths together. A value formatted one way when indexed and another way when queried (culture, case, date format, tokenization) matches nothing. The same goes for the read path: every result mapper has to handle the same inputs.
- Put a rule that all providers share in one shared helper, and its test in `BaseTests`. Don't paste the same expression into each translator. Copies drift apart and get fixed one at a time.
- Format values with `CultureInfo.InvariantCulture`. Never call bare `ToString()` on a query value or a field value.

### Errors and the `Result` contract

- Keep the `try` around **all** of the work, including client creation and settings resolution. A client lookup outside the `try` brings back exceptions you fixed earlier. Guard against `null` inputs (`Fields`, list elements, batch entries) instead of letting them throw.
- Let cancellation propagate, and keep the cause: `catch (Exception e) when (e is not OperationCanceledException) { return Result.Fail(new Error(e.Message).CausedBy(e)); }`.
- Don't fold a server error into a "no" answer. `Ok(false)` and `Ok` are only for answers the engine actually gave (for example a 404, or "already exists"). A 401, 403 or 5xx is a failed `Result`. A failed check must not fall through into the next step, such as attempting a create after a failed exists check.
- Check the engine's limits up front (paging window, terms count, request size) and return a failed `Result` that names the limit, instead of passing on a raw engine error.
- Log at `Error` only on paths that really fail, after the benign cases have been ruled out.

### Configuration and clients

- Never change options objects you get from `IOptions`/`IOptionsMonitor`. Merge into a copy.
- Reuse clients. Cache one client per distinct connection and credential key, not one per call and not one per `Indexes` entry. Keep credentials out of cache-key `ToString()`.
- Lookups by index name are case-insensitive, and they must stay that way even when a caller assigns a whole new `Indexes` dictionary.

### Public API

- Make new types `internal` unless a consumer needs them. That includes helpers shared between providers, which can be shared source or `InternalsVisibleTo`.
- A new member on a public interface breaks every implementer. Give it a default implementation, or call the break out in the changeset.

### Tests that can actually fail

- **Break the code and run the test again.** It must fail. For a bug fix, revert the fix. For a new feature, where a revert would remove the API the test needs, break the implementation instead (return the wrong value, drop a clause, skip the guard). If the test still passes, it isn't testing the change. Say in the PR that you checked this.
- Make the assertion tell outcomes apart. Pick data where the correct and the incorrect behavior give different results. Don't sort results in a test about ordering. When you assert an error, check its message, using text only that error contains, not just `IsFailed`. Don't skip an assertion behind `TryGetValue`, because then a missing field passes.
- A test's name must match what it does. A test named "nested" has to nest, and a test named "changes X" has to change X.
- Use fixed inputs: no `DateTime.UtcNow` or random values in assertions.
- Give each test its own index (`$"name-{Guid.NewGuid():N}"`). Delete it in `finally`, using `CancellationToken.None`, and catch any failure there so the cleanup can't hide the real failure.
- Pass `TestContext.Current.CancellationToken` to async calls (xUnit1051), and assert on the responses of setup calls such as `RefreshAsync`.
- Before you write a fake, a `SearchIdsAsync` helper or a culture-swap block, look for an existing one. Put a test of provider-agnostic code in a provider-agnostic test project, not in a provider's tests.
- If you touch Elasticsearch, OpenSearch or Meilisearch, run their integration tests (Docker). Translation tests alone don't show that the engine accepts the query. If you couldn't run them, say so in the PR.

### Docs, changesets and PR descriptions must match the code

Reviews catch more overstated claims than anything else. Read every sentence you write in the feature matrix, a provider page, a changeset or a PR description against the code:

- Only write "every provider", "always" or "never" when a shared test proves it. Name the API that gives the guarantee (`IndexingServiceAsync` checks things that a direct provider call skips).
- A provider difference that a test asserts must be in the feature matrix and on the provider page.
- When a fix changes behavior that a doc already describes, find and update that sentence. Search `docs/` for the method name.
- The PR description must list every behavior change, above all the riskiest one, and say which test suites ran.

### Housekeeping

- Save Markdown, the changeset and `.csproj` files **without a UTF-8 BOM**. Only `*.cs`/`*.vb` use a BOM. Don't commit changes that only touch encoding or whitespace.
- Merge `main` (or, for a stacked PR, its base branch) into your branch before review, and check other open PRs that touch the same docs or methods. Two PRs can merge without a textual conflict and still contradict each other.
- Keep the PR to its issue. Open a follow-up issue for a valid finding that's out of scope, and link it from the thread.

### Stacked pull requests

We prefer stacked PRs on GitHub. When a change builds on another PR that is still open, don't wait for it, copy its code, or fold both into one PR. Branch from that PR's branch, and open your PR with its **base set to that branch**.

- **Split large work into a stack.** Put a shared refactor or new interface member first and each feature or provider on top. Every PR in the stack covers one issue, stays reviewable on its own, and has its own changeset.
- **Say where the PR sits.** Start the description with `Stacked on #N`, list the whole stack in order, and keep the list current in each PR as the stack changes.
- **Keep the stack up to date by merging, not rebasing.** When a lower PR changes, merge its branch into the one above, and pass the change up the stack one PR at a time.
- **Merge from the bottom up.** Merge the lowest PR into `main` first, and never merge an upper PR into its base branch. A squash merge gives `main` a new commit, so the next PR shows the lower PR's changes again. Once GitHub retargets it to `main`, or you do, merge `main` into its branch so its diff shows only its own change.
- **Review in the context of the stack.** Review each PR against its base, and check that the top of the stack builds and passes the tests on its own. A fix found in a lower PR belongs in that PR, not in the one above it.

## Versioning and releases

Releases are driven by [changesets](https://github.com/changesets/changesets) in **pre mode** (see [`.changeset/README.md`](./.changeset/README.md)). Every PR that changes a package adds a changeset bumping `bielu-bettersearch` (`npx changeset`); chore-only PRs use `npx changeset add --empty`. CI enforces this (`changeset-check`), except on Dependabot and Version Packages PRs. Merging to `main` publishes a beta when shipped code changed.

## Before opening a PR

1. `dotnet build` succeeds with no new warnings.
2. `dotnet test` passes.
3. `dotnet format --verify-no-changes src/Bielu.BetterSearch.slnx` reports nothing; CI checks this on every pull request.
4. A changeset is included.
5. `README.md`, `docs/articles/feature-matrix.md` and the provider pages in `docs/articles/providers/` are updated when behavior or public APIs change, and every claim in them has been checked against the code.
6. You have gone through [Common review findings](#common-review-findings). You have worked through each logic scenario that applies, every provider has been checked and either has the change or the PR explains why not (an engine that can't support it returns a failed `Result` that the feature matrix records), each new test fails when the code it covers is broken, and the integration tests of every engine you touched pass.
7. The branch is up to date with its base (`main`, or the branch of the PR it is stacked on), and the PR description lists every behavior change.
8. Target the `main` branch, or, for a [stacked PR](#stacked-pull-requests), the branch of the PR below it.
