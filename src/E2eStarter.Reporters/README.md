# E2eStarter.Reporters

A `dotnet tool` that turns a TRX test result (and the failure evidence written by `E2eStarter.Playwright`) into a **Playwright test dashboard** and a standalone HTML report.

```bash
dotnet tool install E2eStarter.Reporters --global
dotnet test --project tests/MyApp.E2E --report-trx --report-trx-filename results.trx --results-directory out
e2e-report --trx out/results.trx --artifacts <evidence folder> --out out --title "My app" --env staging
```

The test project needs the `Microsoft.Testing.Extensions.TrxReport` package. The tool writes `data.json`, `index.html` (the dashboard) and `report.html` (standalone and shareable, with screenshots, steps, console output and traces). Pass `--previous <data.json>` to mark new and still failing tests.

See the [repository README](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter) for the full setup.
