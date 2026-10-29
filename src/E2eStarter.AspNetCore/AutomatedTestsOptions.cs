using System.Text.Json;

namespace E2eStarter.AspNetCore;

public sealed class AutomatedTestsOptions
{
    // Off by default. Turn it on only where running tests is acceptable (never in Production).
    public bool Enabled { get; set; }

    // Where run folders are created: {RunsDirectory}/{runId}/.
    public string RunsDirectory { get; set; } = "artifacts/runs";

    // Executable started for every run as: Command {runFolder} [category]. It must write data.json and index.html
    // into the run folder (see scripts/run-ci.sh and the e2e-report tool).
    public string Command { get; set; } = "scripts/run-ci.sh";

    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

    public IDictionary<string, string> Environment { get; } = new Dictionary<string, string>();
}

public enum RunStatus
{
    Running,
    Passed,
    Failed,
    Error,
}

public sealed record RunInfo(
    string Id,
    RunStatus Status,
    string? Category,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int? Total,
    int? Passed,
    int? Failed,
    int? Skipped,
    int? ExitCode);

public sealed record StartRunRequest(string? Category);

internal static class RunJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
