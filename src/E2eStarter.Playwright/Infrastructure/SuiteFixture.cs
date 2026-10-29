using E2eStarter.Playwright.Configuration;
using Microsoft.Playwright;
using Xunit;

namespace E2eStarter.Playwright.Infrastructure;

// One Playwright driver and one browser for the whole run; each test gets its own isolated context.
// Register it in the test assembly: [CollectionDefinition(UiTestBase.CollectionName)] class C : ICollectionFixture<SuiteFixture>.
public sealed class SuiteFixture : IAsyncLifetime
{
    public TestSettings Settings { get; } = SettingsLoader.Load();

    public string ArtifactsRoot => RunArtifacts.Root(Settings);

    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        var launch = new BrowserTypeLaunchOptions
        {
            Headless = Settings.Browser.Headless,
            SlowMo = Settings.Browser.SlowMoMs,
        };

        Browser = Settings.Browser.Name.ToLowerInvariant() switch
        {
            "firefox" => await _playwright.Firefox.LaunchAsync(launch),
            "webkit" => await _playwright.Webkit.LaunchAsync(launch),
            "chromium" => await _playwright.Chromium.LaunchAsync(launch),
            var channel => await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = launch.Headless,
                SlowMo = launch.SlowMo,
                Channel = channel,
            }),
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}
