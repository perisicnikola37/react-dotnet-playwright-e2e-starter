using System.Net;
using System.Net.Http.Json;

namespace E2eStarter.AspNetCore.Tests;

public sealed class RunEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Nothing_is_mapped_in_Production()
    {
        await using var app = await TestApp.StartAsync("Production");

        var response = await app.Client.GetAsync($"{TestApp.Prefix}/runs", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Production_can_be_allowed_explicitly()
    {
        await using var app = await TestApp.StartAsync("Production", o => o.AllowInProduction = true);

        var response = await app.Client.GetAsync($"{TestApp.Prefix}/runs", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_run_reports_its_counts_and_a_string_status()
    {
        await using var app = await TestApp.StartAsync(configure: o =>
            o.Environment["FAKE_DATA"] = FakeData.Of(("A.B.One", "passed"), ("A.B.Two", "skipped")));

        var started = await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { category = "Smoke" }, Ct);
        var run = await started.Content.ReadFromJsonAsync<RunDto>(Ct);
        var done = await app.WaitForAsync(run!.Id);
        var raw = await app.Client.GetStringAsync($"{TestApp.Prefix}/runs/{run.Id}", Ct);

        started.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        done.ShouldBe(done with { Status = "passed", Category = "Smoke", Total = 2, Passed = 1, Failed = 0, Skipped = 1, ExitCode = 0 });
        raw.ShouldContain("\"status\":\"passed\"");
    }

    [Fact]
    public async Task A_run_without_data_json_ends_as_error_with_its_log()
    {
        await using var app = await TestApp.StartAsync(configure: o => o.Environment["FAKE_EXIT"] = "3");

        var run = await (await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { }, Ct)).Content.ReadFromJsonAsync<RunDto>(Ct);
        var done = await app.WaitForAsync(run!.Id);
        var log = await app.Client.GetStringAsync($"{TestApp.Prefix}/runs/{run.Id}/log", Ct);

        done.Status.ShouldBe("error");
        done.ExitCode.ShouldBe(3);
        log.ShouldContain("fake run of");
    }

    [Fact]
    public async Task Only_one_run_at_a_time()
    {
        await using var app = await TestApp.StartAsync(configure: o => o.Environment["FAKE_SLEEP"] = "1");

        var first = await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { }, Ct);
        var second = await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { }, Ct);
        await app.WaitForAsync((await first.Content.ReadFromJsonAsync<RunDto>(Ct))!.Id);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("{\"category\":\"Smoke; rm -rf /\"}")]
    [InlineData("{\"tests\":[\"--filter-trait x\"]}")]
    [InlineData("{\"tests\":[\"A.B $(whoami)\"]}")]
    public async Task Unsafe_categories_and_test_names_are_rejected(string body)
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.PostAsync(
            $"{TestApp.Prefix}/runs", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rerunning_failed_tests_passes_only_their_method_names_to_the_command()
    {
        await using var app = await TestApp.StartAsync(configure: o => o.Environment["FAKE_DATA"] =
            FakeData.Of(("A.B.Passes", "passed"), ("A.B.Fails", "failed"), ("A.B.Theory(x: 1)", "failed")));
        var first = await (await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { }, Ct)).Content.ReadFromJsonAsync<RunDto>(Ct);
        await app.WaitForAsync(first!.Id);

        var rerun = await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs/{first.Id}/rerun", new { mode = "failed" }, Ct);
        var second = await app.WaitForAsync((await rerun.Content.ReadFromJsonAsync<RunDto>(Ct))!.Id);

        rerun.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        second.Tests.ShouldBe(["A.B.Fails", "A.B.Theory"]);
        (await File.ReadAllLinesAsync(Path.Combine(app.RunsRoot, second.Id, "seen-filter.txt"), Ct)).ShouldBe(["A.B.Fails", "A.B.Theory"]);
    }

    [Fact]
    public async Task Rerunning_failed_tests_of_a_green_run_is_a_bad_request()
    {
        await using var app = await TestApp.StartAsync(configure: o => o.Environment["FAKE_DATA"] = FakeData.Of(("A.B.Passes", "passed")));
        var first = await (await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { }, Ct)).Content.ReadFromJsonAsync<RunDto>(Ct);
        await app.WaitForAsync(first!.Id);

        var rerun = await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs/{first.Id}/rerun", new { mode = "failed" }, Ct);

        rerun.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_run_left_running_by_a_stopped_app_is_reported_as_error()
    {
        await using var app = await TestApp.StartAsync();
        var folder = Directory.CreateDirectory(Path.Combine(app.RunsRoot, "20260101000000000")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "run.json"), """{"id":"20260101000000000","status":"running","startedAt":"2026-01-01T00:00:00Z"}""", Ct);

        var run = await app.Client.GetFromJsonAsync<RunDto>($"{TestApp.Prefix}/runs/20260101000000000", Ct);

        run!.Status.ShouldBe("error");
    }

    [Fact]
    public async Task Old_runs_are_deleted_when_a_new_one_starts()
    {
        await using var app = await TestApp.StartAsync(configure: o => o.MaxRuns = 2);
        foreach (var id in new[] { "20200101000000", "20200102000000000", "20200103000000000" })
        {
            Directory.CreateDirectory(Path.Combine(app.RunsRoot, id));
        }

        var run = await (await app.Client.PostAsJsonAsync($"{TestApp.Prefix}/runs", new { }, Ct)).Content.ReadFromJsonAsync<RunDto>(Ct);
        await app.WaitForAsync(run!.Id);

        Directory.EnumerateDirectories(app.RunsRoot).Select(Path.GetFileName).Order().ShouldBe(["20200103000000000", run.Id]);
    }
}
