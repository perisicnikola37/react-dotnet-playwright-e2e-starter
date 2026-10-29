using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore;

public static class AutomatedTestsExtensions
{
    // Header the dashboard sends with "Re-run"; a cross-site form cannot set it, so it doubles as CSRF protection.
    public const string RequestedWithHeader = "X-Requested-With";
    public const string RequestedWithValue = "e2e-dashboard";

    private const string Index = "index.html";

    // Inline scripts and styles only: the dashboard is a single self-contained file.
    private const string DashboardPolicy =
        "default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src 'self' data: blob:; " +
        "connect-src 'self'; frame-src 'self'; base-uri 'none'; form-action 'none'; object-src 'none'";

    private static readonly Lazy<string> EmbeddedDashboard = new(() =>
    {
        using var stream = typeof(AutomatedTestsExtensions).Assembly.GetManifestResourceStream("E2eStarter.AspNetCore.dashboard.html")
            ?? throw new InvalidOperationException("The embedded dashboard is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static IServiceCollection AddAutomatedTests(this IServiceCollection services, Action<AutomatedTestsOptions>? configure = null)
    {
        var builder = services.AddOptions<AutomatedTestsOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TestRunService>();
        services.TryAddSingleton<DashboardTokens>();
        return services;
    }

    // Binds AutomatedTestsOptions from configuration, e.g. builder.Configuration.GetSection("AutomatedTests").
    public static IServiceCollection AddAutomatedTests(this IServiceCollection services, IConfiguration configuration, Action<AutomatedTestsOptions>? configure = null)
    {
        services.AddOptions<AutomatedTestsOptions>().Bind(configuration);
        return services.AddAutomatedTests(configure);
    }

    // Maps the endpoints below {prefix}. Nothing is mapped when AutomatedTestsOptions.Enabled is false, or in the
    // Production environment unless AllowInProduction is set.
    // Protect the group like any other API: app.MapAutomatedTests().RequireAuthorization(). The dashboard routes stay
    // anonymous on purpose; they are authorized by the short-lived token that only an authorized caller can obtain.
    public static RouteGroupBuilder MapAutomatedTests(this IEndpointRouteBuilder endpoints, string prefix = "/api/automated-tests")
    {
        var group = endpoints.MapGroup(prefix);
        var services = endpoints.ServiceProvider;
        var options = services.GetRequiredService<IOptions<AutomatedTestsOptions>>().Value;
        if (!options.Enabled)
        {
            return group;
        }

        if (services.GetService<IHostEnvironment>()?.IsProduction() == true && !options.AllowInProduction)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger(typeof(AutomatedTestsExtensions))
                .LogInformation("Automated test endpoints are not mapped in Production. Set AllowInProduction to change that.");
            return group;
        }

        group.MapPost("/runs", (StartRunRequest? request, TestRunService runs) =>
            Started(runs, () => runs.Start(request?.Category, request?.Tests), run => Results.Accepted($"{prefix}/runs/{run.Id}", run)));

        group.MapGet("/runs", (TestRunService runs) => runs.List());

        group.MapGet("/runs/{id}", (string id, TestRunService runs) =>
            runs.Get(id) is { } run ? Results.Ok(run) : Results.NotFound());

        group.MapPost("/runs/{id}/rerun", (string id, RerunRequest? request, TestRunService runs) =>
            Started(runs, () => runs.Rerun(id, request?.Mode ?? RerunMode.All), run => Results.Accepted($"{prefix}/runs/{run.Id}", run)));

        group.MapGet("/runs/{id}/log", (string id, HttpContext http, TestRunService runs) => Log(http, runs, id));

        group.MapPost("/dashboard-token", (DashboardTokens tokens) => tokens.Issue());

        // Everything an iframe loads. {token} replaces the Authorization header an iframe cannot send.
        var dashboard = group.MapGroup("/dashboard/{token}/{id}")
            .AllowAnonymous()
            .AddEndpointFilter(async (context, next) =>
            {
                var values = context.HttpContext.Request.RouteValues;
                var tokens = context.HttpContext.RequestServices.GetRequiredService<DashboardTokens>();
                return tokens.IsValid(values["token"] as string) && TestRunService.IsValidId(values["id"] as string)
                    ? await next(context)
                    : Results.NotFound();
            });

        dashboard.MapGet("/api/run", (string id, HttpContext http, TestRunService runs) =>
        {
            NoStore(http);
            return runs.Get(id) is { } run ? Results.Ok(run) : Results.NotFound();
        });

        dashboard.MapGet("/api/log", (string id, HttpContext http, TestRunService runs) => Log(http, runs, id));

        dashboard.MapPost("/api/rerun", (string id, RerunRequest? request, HttpContext http, TestRunService runs) =>
        {
            NoStore(http);
            return http.Request.Headers[RequestedWithHeader] != RequestedWithValue
                ? Results.BadRequest(new { error = $"Missing {RequestedWithHeader} header." })
                : Started(runs, () => runs.Rerun(id, request?.Mode ?? RerunMode.All), run => Results.Ok(new { runId = run.Id, url = $"../{run.Id}/{Index}" }));
        });

        var types = new FileExtensionContentTypeProvider();
        dashboard.MapGet("/{**path}", (string id, string? path, HttpContext http, TestRunService runs) =>
        {
            path = string.IsNullOrEmpty(path) ? Index : path;
            var root = Path.GetFullPath(runs.FolderOf(id)) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, path));
            if (!full.StartsWith(root, StringComparison.Ordinal) || !Directory.Exists(root))
            {
                return Results.NotFound();
            }

            var isDashboard = path == Index;
            NoStore(http);
            http.Response.Headers.ContentSecurityPolicy = isDashboard ? DashboardPolicy : "sandbox";

            if (File.Exists(full))
            {
                types.TryGetContentType(full, out var contentType);
                return Results.File(full, contentType ?? "application/octet-stream");
            }

            // While a run is going there is no index.html yet: the built-in dashboard follows events.jsonl live.
            return isDashboard ? Results.Content(EmbeddedDashboard.Value, "text/html; charset=utf-8") : Results.NotFound();
        });

        return group;
    }

    private static IResult Started(TestRunService runs, Func<RunInfo?> start, Func<RunInfo, IResult> ok)
    {
        try
        {
            return start() is { } run ? ok(run) : Results.Conflict(new { error = "A run is already in progress." });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult Log(HttpContext http, TestRunService runs, string id)
    {
        NoStore(http);
        var file = Path.Combine(runs.FolderOf(id), TestRunService.LogFile);
        return TestRunService.IsValidId(id) && File.Exists(file)
            ? Results.Text(TestRunService.ReadShared(file), "text/plain; charset=utf-8")
            : Results.NotFound();
    }

    // Run output is never cached, never sniffed, and the token in the URL never leaks through a Referer header.
    private static void NoStore(HttpContext http)
    {
        var headers = http.Response.Headers;
        headers.CacheControl = "no-store";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
    }
}
