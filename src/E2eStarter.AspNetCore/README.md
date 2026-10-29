# E2eStarter.AspNetCore

Endpoints that start automated UI test runs from your ASP.NET Core API, list them, and serve each run's dashboard and evidence.

```bash
dotnet add package E2eStarter.AspNetCore
```

```csharp
builder.Services.AddAutomatedTests(o =>
{
    o.Enabled = !builder.Environment.IsProduction();   // never run tests in Production
    o.Command = "scripts/run-ci.sh";                   // called as: run-ci.sh <runFolder> [category]
});

app.MapAutomatedTests().RequireAuthorization();        // optional: lock the endpoints
```

| Method | Path | Result |
|---|---|---|
| `POST` | `/api/automated-tests/runs` | start a run (`{ "category": "Smoke" }`), `202`; `409` while another run is in progress |
| `GET` | `/api/automated-tests/runs` | all runs, newest first |
| `GET` | `/api/automated-tests/runs/{id}` | one run |
| `GET` | `/api/automated-tests/runs/{id}/log` | runner output |
| `GET` | `/api/automated-tests/runs/{id}/files/{path}` | files of the run folder (`index.html`, `data.json`, evidence) |

Pair it with `E2eStarter.Reporters` (writes the run folder) and `@perisicnikola37/e2e-starter-react` (the page). See the [repository README](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter).
