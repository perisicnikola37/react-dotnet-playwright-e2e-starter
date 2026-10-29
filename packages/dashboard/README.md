# @perisicnikola37/e2e-starter-dashboard

A single-file dashboard and standalone report for automated UI test runs: totals, failures grouped by cause, new vs still failing tests, timeline, slowest tests, search and filters, per-test evidence, light and dark theme.

```bash
npm install @perisicnikola37/e2e-starter-dashboard
```

Copy `dist/dashboard.html` next to a `data.json` as `index.html`, or embed the run data in the `report-data` script tag. See the [root README](../../README.md#2-dashboard) for the data contract.

```bash
node demo/build.mjs --site _site   # demo with dummy data
```
