# React + .NET + Playwright E2E Starter

A production-style **end-to-end testing starter** for a **React** frontend and an **ASP.NET Core (.NET 10)** API, using **Playwright for .NET** and **xUnit v3**. Clone it, swap the sample "Items" app for your own, and keep a working test framework: page objects, per-test browser isolation, API-based test data, failure evidence and CI.

[![E2E](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/actions/workflows/e2e.yml/badge.svg)](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/actions/workflows/e2e.yml)

## Features

- **Playwright (C#)** with auto-waiting locators; Chromium, Firefox, WebKit, or system Chrome / Edge.
- **Page Object Model**: locators stay private in `Pages/`, tests read as user intent.
- **Isolated tests**: a fresh browser context per test; data is seeded and cleaned up through the API (`ApiClient`, `AfterTest`).
- **Failure evidence**: screenshot, page HTML, URL and a Playwright **trace** per failed test. Passing tests leave nothing.
- **Test categories** (`Smoke`, `Regression`) via xUnit traits.
- **Layered configuration**: `appsettings.json`, optional `appsettings.{E2E_ENVIRONMENT}.json`, then `E2E_*` environment variables.
- **One-command runner** (`scripts/run-e2e.sh`) that starts the API and the web app, waits for them, runs the tests and stops everything.
- **GitHub Actions** workflow that builds, runs the tests and uploads evidence on failure.

## Quick start

Requirements: .NET 10 SDK, Node.js 22+, and a browser (Playwright's Chromium or system Chrome).

```bash
# 1. Install web dependencies
npm ci --prefix src/web

# 2. Build, then install Playwright's browser once
dotnet build
pwsh tests/E2eStarter.E2E/bin/Debug/net10.0/playwright.ps1 install chromium   # or: --browser chrome to use installed Chrome

# 3. Run everything (starts API + web app for you)
scripts/run-e2e.sh
scripts/run-e2e.sh --category Smoke --headed
scripts/run-e2e.sh --browser chrome
```

Ports: API `5080`, web `5180`. The script refuses to run if something already answers there, so tests never hit the wrong app.

Run against an app you already started (`dotnet run --project src/Api`, `npm run dev --prefix src/web`):

```bash
dotnet test --project tests/E2eStarter.E2E
dotnet test --project tests/E2eStarter.E2E --filter-trait "Category=Smoke"
```

## Project layout

```text
src/Api/                      ASP.NET Core minimal API (in-memory "items" CRUD + /health)
src/web/                      React 19 + Vite + TypeScript app (the system under test)
tests/E2eStarter.E2E/
  Configuration/              typed settings + loader
  Infrastructure/             SuiteFixture (one browser), UiTestBase (context, trace, evidence), ApiClient, categories
  Pages/                      page objects
  Tests/Smoke, Tests/Regression
scripts/run-e2e.sh            start apps, run tests, clean up
.github/workflows/e2e.yml     CI
```

## Configuration

| Setting | Environment variable | Default |
| :-- | :-- | :-- |
| Frontend URL | `E2E_BaseUrl` | `http://localhost:5180/` |
| API URL | `E2E_ApiBaseUrl` | `http://localhost:5080` |
| Browser (`Chromium`, `Firefox`, `WebKit`, `chrome`, `msedge`) | `E2E_Browser__Name` | `Chromium` |
| Headless | `E2E_Browser__Headless` | `true` |
| Slow motion (ms) | `E2E_Browser__SlowMoMs` | `0` |
| Default timeout (s) | `E2E_Timeouts__DefaultSeconds` | `10` |
| Trace every test | `E2E_Artifacts__Trace` | `true` |
| Environment file (`appsettings.{name}.json`) | `E2E_ENVIRONMENT` | none |
| Run id (artifact folder) | `E2E_RUN_ID` | timestamp |

## Adding a test

1. Add or extend a page object in `Pages/` (private locators, intent-level methods).
2. Derive from `UiTestBase`, tag the class with `[Trait(TestCategories.Key, TestCategories.Smoke)]`.
3. Arrange through `Api`, act through page objects, assert in the test. Register cleanup with `AfterTest(...)`.

Rules of thumb: everything `async`, no `Thread.Sleep`, no selectors in tests. Prefer locators in this order: role / label, `data-testid`, then CSS.

## Debugging a failure

Failed tests write to `tests/E2eStarter.E2E/bin/<config>/net10.0/artifacts/<runId>/<test>/`: `screenshot.png`, `page.html`, `url.txt`, `trace.zip`.

```bash
pwsh tests/E2eStarter.E2E/bin/Debug/net10.0/playwright.ps1 show-trace path/to/trace.zip
```

## Make it yours

Replace `src/Api` and `src/web` with your own apps (or point `E2E_BaseUrl` / `E2E_ApiBaseUrl` at a deployed environment), then replace `ItemsPage` and the tests. Never run tests that create or delete data against production.

## License

[MIT](LICENSE)
