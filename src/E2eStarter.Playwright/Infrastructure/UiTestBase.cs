using E2eStarter.Playwright.Configuration;
using Microsoft.Playwright;
using Xunit;

namespace E2eStarter.Playwright.Infrastructure;

// Base class for UI tests. Every test gets a fresh browser context (isolated cookies and storage) and a trace.
// On failure the trace, a screenshot and the page HTML are kept under artifacts/{runId}/{test}/; passing tests leave nothing.
[Collection(CollectionName)]
public abstract class UiTestBase(SuiteFixture suite) : IAsyncLifetime
{
    public const string CollectionName = "Browser";

    private readonly List<Func<Task>> _cleanups = [];
    private IBrowserContext _context = null!;

    protected TestSettings Settings => suite.Settings;

    protected IPage Page { get; private set; } = null!;

    public virtual async ValueTask InitializeAsync()
    {
        _context = await suite.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Settings.BaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            Locale = "en-GB",
        });
        _context.SetDefaultTimeout(Settings.Timeouts.DefaultSeconds * 1000);

        if (Settings.Artifacts.Trace)
        {
            await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = false });
        }

        Page = await _context.NewPageAsync();
    }

    // Registers test-data cleanup; it runs after the failure evidence is captured, in reverse order.
    protected void AfterTest(Func<Task> cleanup) => _cleanups.Add(cleanup);

    public virtual async ValueTask DisposeAsync()
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        var folder = Path.Combine(suite.ArtifactsRoot, SanitizeFileName(TestContext.Current.Test?.TestDisplayName ?? "test"));

        if (failed)
        {
            Directory.CreateDirectory(folder);
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(folder, "screenshot.png"), FullPage = true });
            await File.WriteAllTextAsync(Path.Combine(folder, "page.html"), await Page.ContentAsync());
            await File.WriteAllTextAsync(Path.Combine(folder, "url.txt"), Page.Url);
        }

        for (var i = _cleanups.Count - 1; i >= 0; i--)
        {
            try
            {
                await _cleanups[i]();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Cleanup failed: {ex.Message}");
            }
        }

        if (Settings.Artifacts.Trace)
        {
            await _context.Tracing.StopAsync(failed
                ? new TracingStopOptions { Path = Path.Combine(folder, "trace.zip") }
                : new TracingStopOptions());
        }

        await _context.CloseAsync();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return cleaned.Length > 100 ? cleaned[..100] : cleaned;
    }
}
