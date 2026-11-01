# Changesets

This folder is managed by [changesets](https://github.com/changesets/changesets). Every pull request
that changes a shipped package includes a **changeset**: a small markdown file recording what changed
and how the version should bump. CI aggregates these into an automated "Version Packages" PR.

## Add a changeset

```bash
npx changeset
```

All NuGet packages in this repo share one version (`version.props`), so bump the single
`bielu-bettersearch` entry whichever package changed. Bump levels follow semver:

- **patch**: bug fixes, docs, internal changes with no API impact.
- **minor**: new, backward-compatible features.
- **major**: breaking changes.

Chore-only PRs (CI, formatting, tests) record an empty changeset:

```bash
npx changeset add --empty
```

## Pre mode

The repository is in changesets **pre mode** with the `beta` tag (`.changeset/pre.json`), so merging
the Version Packages PR publishes `1.0.0-beta.N`, not a stable version. While in pre mode, breaking
changes do not need a `major` bump. Run `npx changeset pre exit` when
[`ROADMAP.md`](../ROADMAP.md) says the packages are ready for a release candidate.

## How the NuGet version is applied

Changesets is npm-native, so the NuGet suite is represented by a private placeholder package,
`bielu-bettersearch` (in `build/changeset/nuget-suite`). After `changeset version` runs,
`scripts/apply-nuget-version.mjs` copies its new version into `version.props` and splices its
generated changelog section into the root `CHANGELOG.md`.
