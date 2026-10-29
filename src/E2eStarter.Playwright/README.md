# E2eStarter.Playwright

Reusable [Playwright for .NET](https://playwright.dev/dotnet/) infrastructure for **xUnit v3** UI tests.

- `SuiteFixture`: one Playwright driver and browser for the whole run (Chromium, Firefox, WebKit, or `chrome` / `msedge`).
- `UiTestBase`: a fresh browser context and trace per test; on failure it keeps `screenshot.png`, `page.html`, `url.txt` and `trace.zip`. `AfterTest(...)` registers cleanup.
- `SettingsLoader` / `TestSettings`: `appsettings.json` < `appsettings.{E2E_ENVIRONMENT}.json` < `E2E_*` environment variables.
- `TestCategories`: `Smoke` / `Regression` trait values.

## Use

```bash
dotnet add package E2eStarter.Playwright
```

```csharp
using E2eStarter.Playwright.Infrastructure;

[CollectionDefinition(UiTestBase.CollectionName)]
public sealed class BrowserCollection : ICollectionFixture<SuiteFixture>;

[Trait(TestCategories.Key, TestCategories.Smoke)]
public sealed class HomeTests(SuiteFixture suite) : UiTestBase(suite)
{
    [Fact]
    public async Task Home_page_loads()
    {
        await Page.GotoAsync("/");
        (await Page.TitleAsync()).ShouldNotBeEmpty();
    }
}
```

Packages are published to GitHub Packages (`https://nuget.pkg.github.com/perisicnikola37/index.json`), which needs a token with `read:packages`. Full documentation: <https://github.com/perisicnikola37/react-dotnet-playwright-e2e-starter>.
