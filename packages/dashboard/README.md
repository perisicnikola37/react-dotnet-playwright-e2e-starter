# @perisicnikola37/e2e-starter-dashboard

A single-file dashboard and standalone report for automated UI test runs: totals, failures grouped by cause, new vs still failing tests, timeline, slowest tests, search and filters, per-test evidence, light and dark theme.

```bash
npm install @perisicnikola37/e2e-starter-dashboard
```

Copy `dist/dashboard.html` next to a `data.json` as `index.html`, or embed the run data in the `report-data` script tag. The `e2e-report` dotnet tool (`E2eStarter.Reporters`) does this for you: it converts a TRX plus failure evidence into `data.json`, `index.html` and a standalone `report.html`. `data.json` may also carry optional `pageHtml` and `trace` paths per test. See the [root README](../../README.md#5-dashboard) for the data contract.

```bash
node demo/build.mjs --site _site   # demo with dummy data
```
