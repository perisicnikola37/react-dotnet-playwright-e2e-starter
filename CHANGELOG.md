# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-29

### Added

- Sample ASP.NET Core (.NET 10) API and React 19 + Vite app as the system under test.
- Playwright for .NET tests on xUnit v3: page objects, isolated context and trace per test, failure evidence, `Smoke` / `Regression` categories.
- NuGet package `E2eStarter.Playwright` with the reusable test infrastructure.
- npm package `@perisicnikola37/e2e-starter-client`, a typed client for the sample API.
- `scripts/run-e2e.sh` to start the apps, run the tests and clean up.
- CI (build, tests, E2E, CodeQL, commitlint), Dependabot and a tag-driven publish workflow.

[1.0.0]: https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter/releases/tag/v1.0.0
