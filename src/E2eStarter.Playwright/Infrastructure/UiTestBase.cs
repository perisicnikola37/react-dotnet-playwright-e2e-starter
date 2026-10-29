using System.Text.Json;
using E2eStarter.Playwright.Configuration;
using Microsoft.Playwright;
using Xunit;

namespace E2eStarter.Playwright.Infrastructure;

// Base class for UI tests. Every test gets a fresh browser context (isolated cookies and storage) and a trace.
// On failure the screenshot, page HTML, URL, trace, browser console errors and the named steps are kept under
// artifacts/{runId}/{test}/; passing tests leave nothing.
[Collection(CollectionName)]
public abstract class UiTestBase(SuiteFixture suite) : IAsyncLifetime
{
    public const string CollectionName = "Browser";

    private readonly List<Func<Task>> _cleanups = [];
    private readonly List<string> _console = [];
    private readonly List<string> _steps = [];
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
        Page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                lock (_console)
                {
                    _console.Add($"[{message.Type}] {message.Text}");
                }
            }
        };
        Page.PageError += (_, error) =>
        {
            lock (_console)
            {
                _console.Add($"[pageerror] {error}");
            }
        };
    }

    // Names what the test is doing; on failure the dashboard lists the steps and marks the last one as failed.
    protected void Step(string description) => _steps.Add(description);

    // Registers test-data cleanup; it runs after the failure evidence is captured, in reverse order.
    protected void AfterTest(Func<Task> cleanup) => _cleanups.Add(cleanup);

    public virtual async ValueTask DisposeAsync()
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        var folder = RunArtifacts.FolderFor(Settings, TestContext.Current.Test?.TestDisplayName ?? "test");

        if (failed)
        {
            Directory.CreateDirectory(folder);
            await TryAsync(() => Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(folder, RunArtifacts.Screenshot), FullPage = true }));
            await TryAsync(async () => await File.WriteAllTextAsync(Path.Combine(folder, RunArtifacts.PageHtml), await Page.ContentAsync()));
            await File.WriteAllTextAsync(Path.Combine(folder, RunArtifacts.Url), Page.Url);

            string[] console;
            lock (_console)
            {
                console = [.. _console];
            }

            if (console.Length > 0)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, RunArtifacts.Console), JsonSerializer.Serialize(console));
            }

            if (_steps.Count > 0)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, RunArtifacts.Steps), JsonSerializer.Serialize(_steps));
            }
        }

        for (var i = _cleanups.Count - 1; i >= 0; i--)
        {
            await TryAsync(_cleanups[i], "Cleanup");
        }

        if (Settings.Artifacts.Trace)
        {
            await _context.Tracing.StopAsync(failed
                ? new TracingStopOptions { Path = Path.Combine(folder, RunArtifacts.Trace) }
                : new TracingStopOptions());
        }

        await _context.CloseAsync();
    }

    // Evidence and cleanup are best effort: a crashed page must not hide the original test failure.
    private static async Task TryAsync(Func<Task> action, string what = "Capturing evidence")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{what} failed: {ex.Message}");
        }
    }
}
