# @perisicnikola37/e2e-starter-react

A drop-in React page that starts automated UI test runs, lists them and shows the live dashboard of the selected run in an iframe. It talks to the endpoints of `E2eStarter.AspNetCore`.

```bash
npm install @perisicnikola37/e2e-starter-react
```

```tsx
import { AutomatedTestsPage } from "@perisicnikola37/e2e-starter-react";

// Any router works; this is the page for your /automated-tests route.
<AutomatedTestsPage categories={["Smoke", "Regression"]} />
```

Use `createAutomatedTestsClient({ baseUrl, prefix, fetch })` for a custom origin or authentication. React 18+ is a peer dependency.

See the [repository README](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter) for the full setup.
