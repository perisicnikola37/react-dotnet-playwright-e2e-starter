using System.Text.Json;
using E2eStarter.E2E.Pages;
using E2eStarter.Playwright.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace E2eStarter.E2E.Tests.Dashboard;

// The dashboard page itself (packages/dashboard/dashboard.html), with fake run data; needs neither the API nor the web app.
[Trait(TestCategories.Key, "Dashboard")]
public sealed class DashboardTests(SuiteFixture suite) : UiTestBase(suite)
{
    private const string First = "App.Tests.ItemsTests.Lists_items";
    private const string Second = "App.Tests.ItemsTests.Adds_an_item";

    private static object RunStart() => new
    {
        type = "run-start", runId = "live", title = "Live run", environment = "test", baseUrl = "http://app.test",
        startedAt = DateTimeOffset.UtcNow, tests = new[] { new { name = First, categories = new[] { "Smoke" } }, new { name = Second, categories = new[] { "Smoke" } } },
    };

    private static object Passed(string name) => new { type = "test-end", test = new { name, categories = new[] { "Smoke" }, status = "passed", durationMs = 1200 } };

    private static string Data(params object[] tests) => JsonSerializer.Serialize(new
    {
        title = "Finished run", runId = "20260101000000000", environment = "test", baseUrl = "http://app.test",
        startedAt = "2026-01-01T10:00:00Z", elapsedMs = 5000, tests,
    });

    private static object FailedTest(string name, string screenshot = "evidence/x/screenshot.png") => new
    {
        name, categories = new[] { "Smoke" }, status = "failed", durationMs = 3000, error = "Expected 2 items",
        screenshot, steps = new[] { "Open the page", "Add an item", "Check the list" }, failedStep = 2,
    };

    [Fact]
    public async Task A_live_run_shows_progress_and_switches_to_the_final_report()
    {
        var dashboard = new DashboardHarness(Page);
        dashboard.Files["events.jsonl"] = DashboardHarness.Events(RunStart(), Passed(First), new { type = "test-start", name = Second });

        await dashboard.OpenAsync();

        await Expect(dashboard.Meta).ToContainTextAsync("50%");
        await Expect(dashboard.Meta).ToContainTextAsync("1 / 2 tests");
        await Expect(dashboard.Pill).ToHaveTextAsync("Running · 1/2");
        await Expect(dashboard.Test(Second)).ToContainTextAsync("running…");

        dashboard.Files["data.json"] = Data(
            new { name = First, categories = new[] { "Smoke" }, status = "passed", durationMs = 1200 },
            new { name = Second, categories = new[] { "Smoke" }, status = "passed", durationMs = 800 });

        await Expect(dashboard.Pill).ToHaveTextAsync("All passed", new() { Timeout = 5000 });
        await Expect(dashboard.Meta).ToContainTextAsync("2 / 2 tests");
    }

    [Fact]
    public async Task A_failed_test_shows_its_steps_and_the_step_that_failed()
    {
        var dashboard = new DashboardHarness(Page);
        dashboard.Files["data.json"] = Data(FailedTest(First), new { name = Second, categories = Array.Empty<string>(), status = "passed", durationMs = 10 });

        await dashboard.OpenAsync();

        await Expect(Page.Locator(".box li.bad")).ToHaveTextAsync("3. Check the list");
        await Expect(Page.Locator(".box h5").Filter(new() { HasText = "STEPS" })).ToContainTextAsync("FAILED AT STEP 3");
        await Expect(dashboard.Pill).ToHaveTextAsync("1 failed");
    }

    [Fact]
    public async Task Test_data_is_rendered_as_text_and_unsafe_urls_are_dropped()
    {
        var dashboard = new DashboardHarness(Page);
        var hostile = "App.Tests.X.<img src=x onerror=window.pwned=1>";
        dashboard.Files["data.json"] = Data(FailedTest(hostile, screenshot: "javascript:window.pwned=2"));

        await dashboard.OpenAsync();

        await Expect(Page.Locator(".fi")).ToContainTextAsync("<img src=x");
        (await Page.Locator("img[src=x], img[src^='javascript']").CountAsync()).ShouldBe(0);
        (await Page.EvaluateAsync<bool>("() => window.pwned === undefined")).ShouldBeTrue();
    }

    [Fact]
    public async Task Rerun_failed_from_a_served_dashboard_starts_a_new_run_and_opens_it()
    {
        var dashboard = new DashboardHarness(Page);
        dashboard.Files["run"] = """{"id":"20260101000000000","status":"failed","category":null}""";
        dashboard.Files["data.json"] = Data(FailedTest(First));
        dashboard.OnRerun = route => route.FulfillAsync(new()
        {
            Status = 200, ContentType = "application/json", Body = """{"runId":"20260101000001000","url":"../20260101000001000/index.html"}""",
        });

        await dashboard.OpenAsync(DashboardHarness.ServedRun);
        await dashboard.ReRun("Failed only").ClickAsync();

        await Page.WaitForURLAsync("**/20260101000001000/index.html");
        dashboard.Posts.TryPeek(out var request).ShouldBeTrue();
        request!.Headers["x-requested-with"].ShouldBe("e2e-dashboard");
        request.PostData.ShouldBe("""{"mode":"failed"}""");
    }
}
