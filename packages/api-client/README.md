# @perisicnikola37/e2e-starter-client

Typed, dependency-free `fetch` client for the items API of the
[React + .NET + Playwright E2E starter](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter).

```bash
npm install @perisicnikola37/e2e-starter-client
```

```ts
import { createItemsClient, ApiError } from "@perisicnikola37/e2e-starter-client";

const items = createItemsClient({ baseUrl: "http://localhost:5080" });

const created = await items.create("First item");
console.log(await items.list());
await items.remove(created.id);
```

Errors are thrown as `ApiError` (`message` comes from the API, `status` is the HTTP status).

Published to GitHub Packages. Add to your `.npmrc`:

```
@perisicnikola37:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GITHUB_PACKAGES_TOKEN}
```
