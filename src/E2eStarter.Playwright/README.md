# E2eStarter.Playwright

Reusable [Playwright for .NET](https://playwright.dev/dotnet/) infrastructure for **xUnit v3** UI tests, ready for the live test dashboard of [react-dotnet-playwright-e2e-starter](https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter).

- `SuiteFixture`: one Playwright driver and browser for the whole run (Chromium, Firefox, WebKit, or `chrome` / `msedge`).
- `UiTestBase`: a fresh browser context and trace per test; on failure it keeps `screenshot.png`, `page.html`, `url.txt`, `console.json`, `steps.json` and `trace.zip`. `Step(...)` names steps, `AfterTest(...)` registers cleanup.
- `LiveProgressTestFramework`: writes live progress to `events.jsonl` when `E2E_LiveEventsPath` is set.
- `SettingsLoader` / `TestSettings`: `appsettings.json` < `appsettings.{E2E_ENVIRONMENT}.json` < `E2E_*` environment variables.
- `TestCategories`: `Smoke` / `Regression` trait values.

## Use

```bash
dotnet add package E2eStarter.Playwright
```

```csharp
using E2eStarter.Playwright.Infrastructure;
using E2eStarter.Playwright.Reporting;

[assembly: TestFramework(typeof(LiveProgressTestFramework))]

[CollectionDefinition(UiTestBase.CollectionName)]
public sealed class BrowserCollection : ICollectionFixture<SuiteFixture>;

[Trait(TestCategories.Key, TestCategories.Smoke)]
public sealed class HomeTests(SuiteFixture suite) : UiTestBase(suite)
{
    [Fact]
    public async Task Home_page_loads()
    {
        Step("Open the home page");
        await Page.GotoAsync("/");
        (await Page.TitleAsync()).ShouldNotBeEmpty();
    }
}
```

Full documentation: <https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter>.
