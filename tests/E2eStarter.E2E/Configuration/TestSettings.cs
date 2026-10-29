namespace E2eStarter.E2E.Configuration;

public sealed class TestSettings
{
    public string BaseUrl { get; init; } = "http://localhost:5173/";

    public string ApiBaseUrl { get; init; } = "http://localhost:5041";

    public BrowserSettings Browser { get; init; } = new();

    public TimeoutSettings Timeouts { get; init; } = new();

    public ArtifactSettings Artifacts { get; init; } = new();
}

public sealed class BrowserSettings
{
    // Chromium, Firefox, WebKit, or a branded channel: Chrome, Msedge.
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
