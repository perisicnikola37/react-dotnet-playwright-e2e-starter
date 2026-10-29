using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2eStarter.AspNetCore;

public sealed class AutomatedTestsOptions
{
    // Switch to turn the endpoints off without removing the code. Production is blocked separately (AllowInProduction).
    public bool Enabled { get; set; } = true;

    // Running tests from a production app is almost never what you want, so MapAutomatedTests maps nothing there.
    public bool AllowInProduction { get; set; }

    // Where run folders are created: {RunsDirectory}/{runId}/.
    public string RunsDirectory { get; set; } = "artifacts/runs";

    // Executable started for every run as: Command {runFolder} [category]. It must write data.json into the run folder
    // (see scripts/run-ci.sh and the e2e-report tool) and may stream events.jsonl for the live view.
    public string Command { get; set; } = "scripts/run-ci.sh";

    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(15);

    // Title shown on the dashboard; passed to the command as E2E_Title.
    public string Title { get; set; } = "UI tests";

    // Older run folders are deleted when a new run starts. 0 keeps everything.
    public int MaxRuns { get; set; } = 20;

    // How long a dashboard token (used in iframe URLs instead of a bearer token) stays valid.
    public TimeSpan DashboardTokenLifetime { get; set; } = TimeSpan.FromHours(2);

    // Extra environment variables for the command.
    public IDictionary<string, string> Environment { get; } = new Dictionary<string, string>();
}

// Serialized as "running", "passed", ... with any JsonSerializerOptions, including the minimal API defaults.
[JsonConverter(typeof(CamelCaseEnumConverter<RunStatus>))]
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
    int? ExitCode,
    IReadOnlyList<string>? Tests = null);

// Tests: fully qualified test method names (Namespace.Class.Method) to run instead of the whole suite.
public sealed record StartRunRequest(string? Category, IReadOnlyList<string>? Tests = null);

[JsonConverter(typeof(CamelCaseEnumConverter<RerunMode>))]
public enum RerunMode
{
    All,
    Failed,
    Same,
}

public sealed record RerunRequest(RerunMode Mode = RerunMode.All);

public sealed record DashboardToken(string Token, DateTimeOffset ExpiresAt);

public sealed class CamelCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where TEnum : struct, Enum;

internal static class RunJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
