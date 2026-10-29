using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore;

public static class AutomatedTestsExtensions
{
    public static IServiceCollection AddAutomatedTests(this IServiceCollection services, Action<AutomatedTestsOptions>? configure = null)
    {
        var builder = services.AddOptions<AutomatedTestsOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        services.AddSingleton<TestRunService>();
        return services;
    }

    // Maps the endpoints below {prefix}. Nothing is mapped unless AutomatedTestsOptions.Enabled is true.
    // The returned group lets you require authorization: app.MapAutomatedTests().RequireAuthorization().
    public static RouteGroupBuilder MapAutomatedTests(this IEndpointRouteBuilder endpoints, string prefix = "/api/automated-tests")
    {
        var group = endpoints.MapGroup(prefix);
        if (!endpoints.ServiceProvider.GetRequiredService<IOptions<AutomatedTestsOptions>>().Value.Enabled)
        {
            return group;
        }

        var types = new FileExtensionContentTypeProvider();

        group.MapPost("/runs", (StartRunRequest? request, TestRunService runs) =>
        {
            try
            {
                var run = runs.Start(request?.Category);
                return run is null
                    ? Results.Conflict(new { error = "A run is already in progress." })
                    : Results.Accepted($"{prefix}/runs/{run.Id}", run);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/runs", (TestRunService runs) => runs.List());

        group.MapGet("/runs/{id}", (string id, TestRunService runs) =>
            runs.Get(id) is { } run ? Results.Ok(run) : Results.NotFound());

        group.MapGet("/runs/{id}/log", (string id, TestRunService runs) =>
        {
            var file = Path.Combine(runs.FolderOf(id), "runner.log");
            return TestRunService.IsValidId(id) && File.Exists(file) ? Results.File(file, "text/plain") : Results.NotFound();
        });

        // Serves the run folder (index.html, data.json, evidence/*). Relative URLs in index.html resolve below /files/.
        group.MapGet("/runs/{id}/files/{**path}", (string id, string? path, TestRunService runs) =>
        {
            if (!TestRunService.IsValidId(id))
            {
                return Results.NotFound();
            }

            var root = Path.GetFullPath(runs.FolderOf(id)) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, string.IsNullOrEmpty(path) ? "index.html" : path));
            if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full))
            {
                return Results.NotFound();
            }

            types.TryGetContentType(full, out var contentType);
            return Results.File(full, contentType ?? "application/octet-stream");
        });

        return group;
    }
}
