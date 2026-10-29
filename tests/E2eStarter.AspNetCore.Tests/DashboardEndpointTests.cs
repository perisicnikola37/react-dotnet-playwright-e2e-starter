using System.Net;
using System.Net.Http.Json;

namespace E2eStarter.AspNetCore.Tests;

public sealed class DashboardEndpointTests
{
    private const string RunId = "20260101000000000";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> CreateRunFolderAsync(TestApp app, string status = "failed")
    {
        var folder = Directory.CreateDirectory(Path.Combine(app.RunsRoot, RunId)).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "run.json"), $$"""{"id":"{{RunId}}","status":"{{status}}","startedAt":"2026-01-01T00:00:00Z"}""", Ct);
        Directory.CreateDirectory(Path.Combine(folder, "evidence", "A.B.Fails"));
        await File.WriteAllTextAsync(Path.Combine(folder, "evidence", "A.B.Fails", "page.html"), "<script>alert(1)</script>", Ct);
        await File.WriteAllTextAsync(Path.Combine(folder, "data.json"), FakeData.Of(("A.B.Fails", "failed")), Ct);
        return folder;
    }

    private static string Url(string token, string path) => $"{TestApp.Prefix}/dashboard/{token}/{RunId}/{path}";

    [Fact]
    public async Task An_unknown_token_gets_nothing()
    {
        await using var app = await TestApp.StartAsync();
        await CreateRunFolderAsync(app);

        var response = await app.Client.GetAsync(Url(new string('a', 43), "data.json"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_built_in_dashboard_is_served_until_the_run_writes_its_own()
    {
        await using var app = await TestApp.StartAsync();
        await CreateRunFolderAsync(app);

        var response = await app.Client.GetAsync(Url(app.DashboardToken(), "index.html"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("events.jsonl");
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'self'");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Captured_pages_are_sandboxed_so_their_scripts_never_run_on_the_API_origin()
    {
        await using var app = await TestApp.StartAsync();
        await CreateRunFolderAsync(app);

        var response = await app.Client.GetAsync(Url(app.DashboardToken(), "evidence/A.B.Fails/page.html"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldBe("sandbox");
        response.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        response.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
    }

    [Theory]
    [InlineData("../../fake-run.sh")]
    [InlineData("..%2F..%2Ffake-run.sh")]
    public async Task Files_outside_the_run_folder_are_not_served(string path)
    {
        await using var app = await TestApp.StartAsync();
        await CreateRunFolderAsync(app);

        var response = await app.Client.GetAsync(Url(app.DashboardToken(), path), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_dashboard_stays_reachable_by_token_when_the_API_requires_authorization()
    {
        await using var app = await TestApp.StartAsync(requireAuthorization: true);
        await CreateRunFolderAsync(app);

        var runs = await app.Client.GetAsync($"{TestApp.Prefix}/runs", Ct);
        var token = await app.Client.PostAsync($"{TestApp.Prefix}/dashboard-token", null, Ct);
        var dashboard = await app.Client.GetAsync(Url(app.DashboardToken(), "api/run"), Ct);

        runs.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        token.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        dashboard.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rerun_from_the_dashboard_needs_the_requested_with_header_and_returns_the_new_dashboard()
    {
        await using var app = await TestApp.StartAsync();
        await CreateRunFolderAsync(app);
        var token = app.DashboardToken();

        var withoutHeader = await app.Client.PostAsJsonAsync(Url(token, "api/rerun"), new { mode = "failed" }, Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, Url(token, "api/rerun")) { Content = JsonContent.Create(new { mode = "failed" }) };
        request.Headers.Add(AutomatedTestsExtensions.RequestedWithHeader, AutomatedTestsExtensions.RequestedWithValue);
        var withHeader = await app.Client.SendAsync(request, Ct);
        var body = await withHeader.Content.ReadFromJsonAsync<RerunResponse>(Ct);
        await app.WaitForAsync(body!.RunId);

        withoutHeader.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        withHeader.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.Url.ShouldBe($"../{body.RunId}/index.html");
    }

    private sealed record RerunResponse(string RunId, string Url);
}
