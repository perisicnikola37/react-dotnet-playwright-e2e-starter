namespace E2eStarter.Playwright.Configuration;

public sealed class TestSettings
{
    public string BaseUrl { get; init; } = "http://localhost:5180/";

    public string ApiBaseUrl { get; init; } = "http://localhost:5080";

    // Label shown on the dashboard (E2E_ENVIRONMENT also selects appsettings.{name}.json).
    public string Environment { get; init; } = "local";

    // Dashboard title of the run.
    public string Title { get; init; } = "UI tests";

    // events.jsonl for the live dashboard; written only when set and LiveProgressTestFramework is registered.
    public string? LiveEventsPath { get; init; }

    public BrowserSettings Browser { get; init; } = new();

    public TimeoutSettings Timeouts { get; init; } = new();

    public ArtifactSettings Artifacts { get; init; } = new();
}

public sealed class BrowserSettings
{
    // Chromium, Firefox, WebKit, or a branded channel: chrome, msedge.
    public string Name { get; init; } = "Chromium";

    public bool Headless { get; init; } = true;

    public int SlowMoMs { get; init; }
}

public sealed class TimeoutSettings
{
    public int DefaultSeconds { get; init; } = 10;
}

public sealed class ArtifactSettings
{
    public string Directory { get; init; } = "artifacts";

    public bool Trace { get; init; } = true;
}
