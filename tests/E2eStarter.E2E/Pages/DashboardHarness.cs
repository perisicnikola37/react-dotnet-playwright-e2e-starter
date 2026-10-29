using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;

namespace E2eStarter.E2E.Pages;

// Serves packages/dashboard/dashboard.html and fake run files through Playwright routing, so the dashboard can be
// tested without the API or the web app. Files can change while the page is open, like during a real run.
public sealed class DashboardHarness(IPage page)
{
    public const string Origin = "http://dashboard.test";
    public const string ServedRun = "/api/automated-tests/dashboard/token/20260101000000000/";

    private static readonly string Html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dashboard.html"));

    public ConcurrentDictionary<string, string> Files { get; } = new();

    public ConcurrentQueue<IRequest> Posts { get; } = new();

    public Func<IRoute, Task>? OnRerun { get; set; }

    public static string Events(params object[] events) =>
        string.Join('\n', events.Select(e => JsonSerializer.Serialize(e))) + "\n";

    public async Task OpenAsync(string folder = "/run/")
    {
        await page.RouteAsync($"{Origin}/**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (route.Request.Method == "POST")
            {
                Posts.Enqueue(route.Request);
                await (OnRerun?.Invoke(route) ?? route.FulfillAsync(new() { Status = 404 }));
                return;
            }

            if (path.EndsWith("/index.html", StringComparison.Ordinal))
            {
                await route.FulfillAsync(new() { Status = 200, ContentType = "text/html", Body = Html });
                return;
            }

            // Files are keyed by the last path segment: "data.json", "events.jsonl", and "run" or "log" for api/run and api/log.
            var name = path[(path.LastIndexOf('/') + 1)..];
            await (Files.TryGetValue(name, out var body)
                ? route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = body })
                : route.FulfillAsync(new() { Status = 404 }));
        });

        await page.GotoAsync($"{Origin}{folder}index.html");
    }

    public ILocator Meta => page.Locator(".meta");

    public ILocator Pill => page.Locator(".pill");

    public ILocator Test(string name) => page.Locator($"[data-test=\"{name}\"]");

    public ILocator ReRun(string label) => page.Locator("aside .rr").Filter(new() { HasText = label });
}
