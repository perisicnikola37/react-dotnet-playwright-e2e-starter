# E2eStarter.Reporters

A `dotnet tool` that turns a TRX test report (and the failure evidence written by `E2eStarter.Playwright`) into a dashboard.

```bash
dotnet tool install E2eStarter.Reporters --global
dotnet test --project tests/MyApp.E2E --report-trx --report-trx-filename results.trx --results-directory out
e2e-report --trx out/results.trx --artifacts out/artifacts/run --out out/report --title "My app" --env staging
```

The test project needs the `Microsoft.Testing.Extensions.TrxReport` package. The tool writes `data.json`, `index.html` (the live dashboard) and `report.html` (a standalone, shareable report) to the output folder. Pass `--previous <data.json>` to mark new and still failing tests.

See the [repository README](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter) for the full setup.
