# Contributing

Thanks for helping improve the starter. Small, focused pull requests are easiest to review.

## Setup

Requirements: .NET 10 SDK, Node.js 22+, a Playwright browser.

```bash
npm ci
dotnet build E2eStarter.sln
pwsh tests/E2eStarter.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
scripts/run-e2e.sh
```

## Before you open a pull request

- `npm run build` and `npm test --workspace packages/react`
- `dotnet build E2eStarter.sln` and the unit tests: `dotnet test --project tests/E2eStarter.Api.Tests`, `tests/E2eStarter.AspNetCore.Tests` and `tests/E2eStarter.Reporters.Tests`
- `scripts/run-e2e.sh` (full local cycle; add `--report out/report` for the dashboard) and, against a running app, `scripts/run-ci.sh <run folder> [category]` (tests plus `e2e-report`)
- Changes to `packages/dashboard/dashboard.html`: `dotnet test --project tests/E2eStarter.E2E --filter-trait "Category=Dashboard"` (no app needed)

## Commit messages and pull request titles

[Conventional Commits](https://www.conventionalcommits.org/) are enforced by commitlint (a Husky hook locally, and the PR title in CI). Pull requests are **squash-merged**, so the PR title becomes the commit on `main`.

```text
feat: add a login page object
fix: wait for the list before asserting
docs: explain StorageState authentication
```

## Conventions

- Page objects own locators; tests contain intent and assertions only.
- Everything is `async`/`await`; no `Thread.Sleep`.
- Prefer locators in this order: role / label, `data-testid`, then CSS.
- Tests create their own data through the API and remove it with `AfterTest`.

## Releasing (maintainers)

1. Bump `<Version>` in `Directory.Build.props` and `version` in every `packages/*/package.json` (dashboard, react); they must all match.
2. Update `CHANGELOG.md`.
3. Merge, then tag: `git tag vX.Y.Z && git push origin vX.Y.Z`.

The **Publish** workflow checks that the tag matches every version, then publishes the NuGet and npm packages to nuget.org (trusted publishing) and npmjs.com (`NPM_TOKEN` secret) and creates the GitHub Release.
