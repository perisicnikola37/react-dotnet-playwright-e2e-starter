using E2eStarter.E2E.Configuration;
using Microsoft.Playwright;

namespace E2eStarter.E2E.Infrastructure;

// One Playwright driver and one browser for the whole run; each test gets its own isolated context.
public sealed class SuiteFixture : IAsyncLifetime
{
    public TestSettings Settings { get; } = SettingsLoader.Load();

    public string ArtifactsRoot { get; private set; } = string.Empty;

    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var runId = Environment.GetEnvironmentVariable("E2E_RUN_ID") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        ArtifactsRoot = Path.GetFullPath(Path.Combine(Settings.Artifacts.Directory, runId));

        _playwright = await Playwright.CreateAsync();

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

[CollectionDefinition(Name)]
public sealed class BrowserCollection : ICollectionFixture<SuiteFixture>
{
    public const string Name = "Browser";
}
