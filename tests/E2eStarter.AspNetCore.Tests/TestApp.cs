using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore.Tests;

// An app that maps the automated-test endpoints and runs a fake test command instead of real tests.
// The command writes FAKE_DATA as data.json, copies the filter file it got, sleeps FAKE_SLEEP seconds and exits FAKE_EXIT.
public sealed class TestApp : IAsyncDisposable
{
    public const string Prefix = "/api/automated-tests";

    private const string FakeCommand = """
        #!/usr/bin/env bash
        sleep "${FAKE_SLEEP:-0}"
        [[ -n "${AUTOMATED_TESTS_FILTER_FILE:-}" ]] && cp "$AUTOMATED_TESTS_FILTER_FILE" "$1/seen-filter.txt"
        [[ -n "${FAKE_DATA:-}" ]] && printf '%s' "$FAKE_DATA" > "$1/data.json"
        echo "fake run of $1"
        exit "${FAKE_EXIT:-0}"
        """;

    private readonly WebApplication _app;

    private TestApp(WebApplication app, string root)
    {
        _app = app;
        Root = root;
        Client = app.GetTestClient();
    }

    public string Root { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public string RunsRoot => Services.GetRequiredService<TestRunService>().RunsRoot;

    public static async Task<TestApp> StartAsync(
        string environment = "Development",
        Action<AutomatedTestsOptions>? configure = null,
        bool requireAuthorization = false)
    {
        var root = Directory.CreateTempSubdirectory("e2e-aspnet-tests-").FullName;
        var command = Path.Combine(root, "fake-run.sh");
        await File.WriteAllTextAsync(command, FakeCommand.ReplaceLineEndings("\n"));
        // The fake command is a bash script, so these tests run on Linux and macOS (as in CI).
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(command, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAutomatedTests(o =>
        {
            o.WorkingDirectory = root;
            o.Command = command;
            configure?.Invoke(o);
        });
        builder.Services.AddAuthentication("Deny").AddScheme<AuthenticationSchemeOptions, DenyAll>("Deny", _ => { });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        var group = app.MapAutomatedTests(Prefix);
        if (requireAuthorization)
        {
            group.RequireAuthorization();
        }

        await app.StartAsync();
        return new TestApp(app, root);
    }

    public async Task<RunDto> WaitForAsync(string id)
    {
        for (var i = 0; i < 100; i++)
        {
            var run = await Client.GetFromJsonAsync<RunDto>($"{Prefix}/runs/{id}", TestContext.Current.CancellationToken);
            if (run!.Status != "running")
            {
                return run;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Run {id} did not finish.");
    }

    public string DashboardToken() => Services.GetRequiredService<DashboardTokens>().Issue().Token;

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // Nobody is ever authenticated, so endpoints that require authorization answer 401.
    private sealed class DenyAll(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}

public sealed record RunDto(string Id, string Status, string? Category, int? Total, int? Passed, int? Failed, int? Skipped, int? ExitCode, string[]? Tests);

public static class FakeData
{
    public static string Of(params (string Name, string Status)[] tests) =>
        System.Text.Json.JsonSerializer.Serialize(new { runId = "x", tests = tests.Select(t => new { name = t.Name, status = t.Status }) });
}
