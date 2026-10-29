# E2eStarter.AspNetCore

Add a **Playwright test dashboard** to your ASP.NET Core API: endpoints that start UI test runs from the browser, follow them live, re-run failed tests and serve the dashboard and evidence of every run. Use it with the React page `@perisicnikola37/e2e-starter-react`.

```bash
dotnet add package E2eStarter.AspNetCore
```

```csharp
builder.Services.AddAutomatedTests(builder.Configuration.GetSection("AutomatedTests"));
app.MapAutomatedTests().RequireAuthorization();   // /api/automated-tests
```

Nothing is mapped in the `Production` environment unless `AllowInProduction` is `true`.

| Method | Path | Result |
|---|---|---|
| `POST` | `/runs` | start a run (`{ "category": "Smoke" }` or `{ "tests": [...] }`), `202`; `409` while a run is in progress |
| `GET` | `/runs` | recent runs, newest first |
| `GET` | `/runs/{id}` | one run |
| `POST` | `/runs/{id}/rerun` | `{ "mode": "all" \| "failed" \| "same" }` |
| `GET` | `/runs/{id}/log` | runner output |
| `POST` | `/dashboard-token` | short-lived token for the dashboard iframe |
| `GET` | `/dashboard/{token}/{id}/{path}` | dashboard and files of the run (sandboxed) |

Options: `Enabled`, `AllowInProduction`, `RunsDirectory`, `Command` (default `scripts/run-ci.sh`, called as `Command <runFolder> [category]`), `WorkingDirectory`, `Timeout`, `Title`, `MaxRuns`, `DashboardTokenLifetime`, `Environment`.

Full setup: <https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter>.
