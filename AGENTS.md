# AGENTS.md

Guidance for AI coding agents working in this repository. Human contributors should also read [CONTRIBUTING.md](./CONTRIBUTING.md).

## What this project is

`Bielu.BetterSearch` is a **provider-agnostic search abstraction** for .NET: a query model and service interfaces, with engine-specific providers behind them. It is in beta. [ROADMAP.md](./ROADMAP.md) is the plan for reaching a stable release, and [FEATURE_MATRIX.md](./FEATURE_MATRIX.md) records what each provider really supports; keep both accurate.

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

## Versioning and releases

Releases are driven by [changesets](https://github.com/changesets/changesets) in **pre mode** (see [`.changeset/README.md`](./.changeset/README.md)). Every PR that changes a package adds a changeset bumping `bielu-bettersearch` (`npx changeset`); chore-only PRs use `npx changeset add --empty`. CI enforces this (`changeset-check`), except on Dependabot and Version Packages PRs. Merging to `main` publishes a beta when shipped code changed.

## Before opening a PR

1. `dotnet build` succeeds with no new warnings.
2. `dotnet test` passes.
3. `dotnet format --verify-no-changes src/Bielu.BetterSearch.slnx` reports nothing new.
4. A changeset is included.
5. `README.md` and `FEATURE_MATRIX.md` are updated when behavior or public APIs change.
6. Target the `main` branch.
