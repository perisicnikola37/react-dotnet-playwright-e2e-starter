# @perisicnikola37/e2e-starter-dashboard

A single-file **Playwright test dashboard** and standalone HTML report: live progress while tests run, totals, failures grouped by cause with screenshots, steps, browser console and traces, new vs still failing tests, time spent, slowest tests, search and filters, re-run buttons, light and dark theme.

[Live demo](https://perisicnikola37.github.io/react-dotnet-playwright-e2e-starter/demo/)

```bash
npm install @perisicnikola37/e2e-starter-dashboard
```

`dist/dashboard.html` reads, in order: data embedded in `<script id="report-data" type="application/json">`, `data.json` next to it, then `events.jsonl` (live mode). The `e2e-report` dotnet tool (`E2eStarter.Reporters`) writes it for you as `index.html` plus a standalone `report.html`; `E2eStarter.AspNetCore` serves it to the React page. See the [root README](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter#dashboard) for the `data.json` contract.

```bash
node demo/build.mjs --site _site   # demo with dummy data
```
