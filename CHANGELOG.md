# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.3.0] - 2026-10-29

### Added

- Live dashboard: `LiveProgressTestFramework` writes `events.jsonl` while tests run; the dashboard shows queued, running, passed and failed tests, progress and time spent before the report exists.
- Re-run from the dashboard and the API (`POST /runs/{id}/rerun` with `all`, `failed` or `same`), and `POST /runs` with a list of tests. Static reports copy the matching `dotnet test --filter-method` command instead.
- Short-lived dashboard tokens (`POST /dashboard-token`) so the iframe works with bearer-token authorization; `getAccessToken` option in `createAutomatedTestsClient`.
- `UiTestBase.Step()` and console capture: failed tests now keep `console.json` and `steps.json`, and the dashboard shows the step that failed.
- `AutomatedTestsOptions`: `AllowInProduction`, `Title`, `MaxRuns`, `DashboardTokenLifetime`, and an `AddAutomatedTests(IConfiguration)` overload.
- Unit tests for `E2eStarter.AspNetCore` and `E2eStarter.Reporters`, and browser tests for the dashboard.
- `scripts/run-e2e.sh --report <dir>`; the E2E workflow uploads the dashboard and `report.html` of every run.
- Landing page on GitHub Pages; the dashboard demo moved to `/demo/`.

### Changed

- **Breaking:** the endpoints are mapped by default (`Enabled` is `true`), but never in the `Production` environment unless `AllowInProduction` is set.
- **Breaking:** run files are served from `/dashboard/{token}/{id}/...` instead of `/runs/{id}/files/...`; the client's `viewUrl()` is replaced by the async `dashboardUrl()`, and `logUrl()` is now async.
- Run status is serialized as a string (`"passed"`) instead of a number.
- Dashboard and evidence files are served with a Content-Security-Policy (`sandbox` for evidence), `nosniff`, `no-store` and `no-referrer`.
- Stale runs left by a restarted API are marked as `error`; old run folders are deleted (`MaxRuns`).
- The dashboard shows real progress and test counts, and replaces the simulated "parallel" re-run options with real re-runs.

### Fixed

- A timed-out run no longer hangs while reading the runner output; the log is capped at 2 MB.
- Evidence of tests whose names contain arguments or special characters is found and linked.
- Reports without `<Times>` in the TRX get start time and duration from the test results.

### Removed

- **Breaking:** `@perisicnikola37/e2e-starter-client`. The typed client of the sample Items API now lives in the sample app (`src/web/src/itemsClient.ts`).

## [1.2.0] - 2026-10-29

### Added

- `E2eStarter.Reporters`: the `e2e-report` dotnet tool that turns a TRX and failure evidence into `data.json`, `index.html` and `report.html`.
- `E2eStarter.AspNetCore`: `AddAutomatedTests` / `MapAutomatedTests` endpoints to start runs and serve their dashboards.
- `@perisicnikola37/e2e-starter-react`: `AutomatedTestsPage` and `createAutomatedTestsClient`.
- `scripts/run-ci.sh`: runs the tests with a TRX report and then `e2e-report`.
- Automated tests page in the sample app.

## [1.1.0] - 2026-10-29

### Added

- `@perisicnikola37/e2e-starter-dashboard`: a single-file live dashboard and standalone report for test runs, with a demo on GitHub Pages.
- Redesigned README with packages table, architecture diagram and data contracts.

### Removed

- The in-app "Automated tests" page of the sample web app (replaced by the dashboard).

## [1.0.0] - 2026-10-29

### Added

- Sample ASP.NET Core (.NET 10) API and React 19 + Vite app as the system under test.
- Playwright for .NET tests on xUnit v3: page objects, isolated context and trace per test, failure evidence, `Smoke` / `Regression` categories.
- NuGet package `E2eStarter.Playwright` with the reusable test infrastructure.
- npm package `@perisicnikola37/e2e-starter-client`, a typed client for the sample API.
- `scripts/run-e2e.sh` to start the apps, run the tests and clean up.
- CI (build, tests, E2E, CodeQL, commitlint), Dependabot and a tag-driven publish workflow.

[1.0.0]: https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/releases/tag/v1.0.0
[1.1.0]: https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/releases/tag/v1.1.0
[1.2.0]: https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/releases/tag/v1.2.0
[1.3.0]: https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/releases/tag/v1.3.0
