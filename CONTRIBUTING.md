# Contributing to Bielu.BetterSearch

## Prerequisites

- **.NET SDK 10.0**
- **Node.js 22.x** or later (for `npx changeset`)
- **Docker** (for the Elasticsearch tests and the Aspire AppHost)

## Build and test

```bash
dotnet build src/Bielu.BetterSearch.slnx
dotnet test --solution src/Bielu.BetterSearch.slnx
dotnet format --verify-no-changes src/Bielu.BetterSearch.slnx
```

## Run the infrastructure

The Aspire AppHost starts Elasticsearch with the dashboard:

```bash
dotnet run --project src/examples/Bielu.BetterSearch.Examples.AppHost
```

## Pull requests

1. Target `main`.
2. Make sure the build adds no new warnings and the tests pass.
3. Update `README.md` and `FEATURE_MATRIX.md` when behavior or public APIs change.
4. Add a changeset: `npx changeset`, or `npx changeset add --empty` for changes that ship nothing. A
   NuGet dependency bump ships in the packages, so it needs a normal changeset. CI fails a PR without
   one, except Dependabot and Version Packages PRs.

## Releases

Releases run from GitHub Actions and are driven by changesets; see
[`.changeset/README.md`](.changeset/README.md). Don't edit `version.props` or `CHANGELOG.md` by hand.

- **Pull requests** build packages with a `-pr` suffix and publish nothing.
- **Pushes to `main` with changesets pending** publish a `-beta.<timestamp>` prerelease.
- **Merging the Version Packages PR** publishes the version committed to `version.props` and creates a
  matching GitHub Release.
