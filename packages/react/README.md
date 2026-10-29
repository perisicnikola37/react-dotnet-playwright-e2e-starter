# @perisicnikola37/e2e-starter-react

Add a **Playwright test dashboard** to your React app: a drop-in page that starts UI test runs, lists them and shows the live dashboard of the selected run, with re-run of failed tests, screenshots, steps and traces. It talks to the endpoints of `E2eStarter.AspNetCore`.

[Live demo of the dashboard](https://perisicnikola37.github.io/react-dotnet-playwright-e2e-starter/demo/)

```bash
npm install @perisicnikola37/e2e-starter-react
```

```tsx
import { AutomatedTestsPage } from "@perisicnikola37/e2e-starter-react";

// Any router works; this is the page for your /automated-tests route.
<AutomatedTestsPage categories={["Smoke", "Regression"]} />
```

Props: `client`, `baseUrl`, `title`, `categories`, `pollMs` (default 3000), `height` (default `80vh`).

For a custom origin or authentication, pass a client:

```tsx
import { AutomatedTestsPage, createAutomatedTestsClient } from "@perisicnikola37/e2e-starter-react";

const client = createAutomatedTestsClient({ baseUrl: "https://api.example.test", getAccessToken: () => getToken() });
<AutomatedTestsPage client={client} />
```

The client also has `listRuns`, `getRun`, `startRun`, `rerun`, `issueDashboardToken`, `dashboardUrl` and `logUrl`. React 18+ is a peer dependency.

See the [repository README](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter) for the full setup.
