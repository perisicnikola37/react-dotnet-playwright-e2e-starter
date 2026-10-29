using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore;

// Starts the test command in the background (one run at a time) and keeps one folder per run:
// run.json (this record), runner.log, filter.txt (selected tests), events.jsonl (live), data.json and evidence/ (results).
public sealed partial class TestRunService(IOptions<AutomatedTestsOptions> options, ILogger<TestRunService>? logger = null)
{
    public const string RunFile = "run.json";
    public const string LogFile = "runner.log";
    public const string DataFile = "data.json";
    public const string EventsFile = "events.jsonl";
    public const string FilterFile = "filter.txt";

    private const long MaxLogBytes = 2_000_000;
    private const int MaxSelectedTests = 1000;

    private readonly AutomatedTestsOptions _options = options.Value;
    private readonly ILogger _logger = logger ?? NullLogger<TestRunService>.Instance;
    private readonly Lock _gate = new();
    private RunInfo? _current;

    public string RunsRoot => Path.GetFullPath(_options.RunsDirectory, Path.GetFullPath(_options.WorkingDirectory));

    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    public string FolderOf(string id) => Path.Combine(RunsRoot, id);

    // Returns null while another run is in progress: runs share the app under test and its data.
    public RunInfo? Start(string? category, IReadOnlyList<string>? tests = null)
    {
        if (category is not null && !CategoryPattern().IsMatch(category))
        {
            throw new ArgumentException("Category may only contain letters, digits, '-' and '_'.", nameof(category));
        }

        var selected = NormalizeTests(tests);
        RunInfo run;
        lock (_gate)
        {
            if (_current is { Status: RunStatus.Running })
            {
                return null;
            }

            var id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            Directory.CreateDirectory(FolderOf(id));
            // The dashboard polls this file from the start; the test framework appends to it once tests begin.
            File.WriteAllText(Path.Combine(FolderOf(id), EventsFile), "");
            run = new RunInfo(id, RunStatus.Running, category, DateTimeOffset.UtcNow, null, null, null, null, null, null, selected);
            _current = run;
            Save(run);
        }

        Prune(run.Id);
        _ = Task.Run(() => ExecuteAsync(run));
        return run;
    }

    // Starts a new run based on an earlier one: the whole suite, only its failed tests, or the same selection again.
    public RunInfo? Rerun(string id, RerunMode mode)
    {
        var run = Get(id) ?? throw new KeyNotFoundException($"Run {id} does not exist.");
        return mode switch
        {
            RerunMode.Failed => FailedTests(id) is { Count: > 0 } failed
                ? Start(null, failed)
                : throw new ArgumentException("That run has no failed tests."),
            RerunMode.Same => Start(run.Category, run.Tests),
            _ => Start(run.Category),
        };
    }

    public IReadOnlyList<RunInfo> List()
    {
        if (!Directory.Exists(RunsRoot))
        {
            return [];
        }

        return RunIds()
            .Select(Get)
            .OfType<RunInfo>()
            .ToList();
    }

    public RunInfo? Get(string id)
    {
        if (!IsValidId(id))
        {
            return null;
        }

        lock (_gate)
        {
            if (_current?.Id == id)
            {
                return _current;
            }
        }

        var file = Path.Combine(FolderOf(id), RunFile);
        RunInfo? run;
        try
        {
            run = File.Exists(file) ? JsonSerializer.Deserialize<RunInfo>(File.ReadAllText(file), RunJson.Options) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }

        // "Running" on disk but not the current run: the app stopped during the run, so it will never finish.
        if (run is { Status: RunStatus.Running })
        {
            run = run with { Status = RunStatus.Error, FinishedAt = run.FinishedAt ?? DateTimeOffset.UtcNow };
            TrySave(run);
        }

        return run;
    }

    // Names of the failed tests, from data.json or, when the reporter did not get to run, from events.jsonl.
    public IReadOnlyList<string> FailedTests(string id)
    {
        if (!IsValidId(id))
        {
            return [];
        }

        try
        {
            var data = Path.Combine(FolderOf(id), DataFile);
            if (File.Exists(data))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(data));
                return doc.RootElement.GetProperty("tests").EnumerateArray()
                    .Where(t => t.GetProperty("status").GetString() == "failed")
                    .Select(t => t.GetProperty("name").GetString()!)
                    .ToList();
            }

            var events = Path.Combine(FolderOf(id), EventsFile);
            if (File.Exists(events))
            {
                return ReadShared(events)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(line => JsonDocument.Parse(line).RootElement)
                    .Where(e => e.GetProperty("type").GetString() == "test-end"
                        && e.GetProperty("test").GetProperty("status").GetString() == "failed")
                    .Select(e => e.GetProperty("test").GetProperty("name").GetString()!)
                    .ToList();
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Could not read the failed tests of run {RunId}", id);
        }

        return [];
    }

    // The test command keeps the file open while it writes, so read it without asking for exclusive access.
    internal static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Test names arrive from the browser; only plain method names are accepted ("(args)" of a theory is dropped).
    private static List<string>? NormalizeTests(IReadOnlyList<string>? tests)
    {
        if (tests is null || tests.Count == 0)
        {
            return null;
        }

        var names = tests
            .Select(t => (t ?? string.Empty).Split('(', 2)[0].Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (names.Count > MaxSelectedTests || names.Any(n => !TestNamePattern().IsMatch(n)))
        {
            throw new ArgumentException("Tests must be fully qualified test method names.", nameof(tests));
        }

        return names;
    }

    private IEnumerable<string> RunIds() =>
        Directory.EnumerateDirectories(RunsRoot)
            .Select(Path.GetFileName)
            .Where(IsValidId)
            .OrderByDescending(id => id, StringComparer.Ordinal)!;

    private void Prune(string keep)
    {
        if (_options.MaxRuns <= 0)
        {
            return;
        }

        foreach (var id in RunIds().Where(id => id != keep).Skip(_options.MaxRuns - 1))
        {
            try
            {
                Directory.Delete(FolderOf(id), recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not delete the old run folder {RunId}", id);
            }
        }
    }

    // A relative path such as scripts/run-ci.sh is resolved against WorkingDirectory, not the process cwd.
    private string ResolveCommand()
    {
        var command = _options.Command;
        var isRelativePath = !Path.IsPathRooted(command) && (command.Contains('/') || command.Contains('\\'));
        return isRelativePath ? Path.GetFullPath(command, Path.GetFullPath(_options.WorkingDirectory)) : command;
    }

    private async Task ExecuteAsync(RunInfo run)
    {
        var folder = FolderOf(run.Id);
        var logPath = Path.Combine(folder, LogFile);
        int? exitCode = null;

        try
        {
            var info = new ProcessStartInfo(ResolveCommand())
            {
                WorkingDirectory = _options.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.ArgumentList.Add(folder);
            if (run.Category is not null)
            {
                info.ArgumentList.Add(run.Category);
            }

            // Build servers would inherit the output pipes and keep them open after the run.
            info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
            info.Environment["E2E_Title"] = _options.Title;

            if (run.Tests is { Count: > 0 })
            {
                var filter = Path.Combine(folder, FilterFile);
                await File.WriteAllLinesAsync(filter, run.Tests);
                info.Environment["AUTOMATED_TESTS_FILTER_FILE"] = filter;
            }

            var previous = List().FirstOrDefault(r => r.Id != run.Id && r.Status is RunStatus.Passed or RunStatus.Failed);
            if (previous is not null)
            {
                info.Environment["E2E_PREVIOUS_DATA"] = Path.Combine(FolderOf(previous.Id), DataFile);
            }

            foreach (var (key, value) in _options.Environment)
            {
                info.Environment[key] = value;
            }

            await using var log = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("The test command did not start.");
            var copy = Task.WhenAll(Pump(process.StandardOutput, log), Pump(process.StandardError, log));

            using var timeout = new CancellationTokenSource(_options.Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Append(log, $"{Environment.NewLine}Stopped: the run took longer than {_options.Timeout}.{Environment.NewLine}");
            }

            // The log is disposed after this, so give the pumps a bounded time to drain what is left.
            await copy.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(_ => { }, TaskScheduler.Default);
            exitCode = process.HasExited ? process.ExitCode : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test run {RunId} could not be executed", run.Id);
            try
            {
                await File.AppendAllTextAsync(logPath, ex.ToString());
            }
            catch (IOException)
            {
            }
        }

        Finish(run, exitCode);
    }

    private static async Task Pump(StreamReader reader, FileStream log)
    {
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer)) > 0)
            {
                Append(log, new string(buffer, 0, read));
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void Append(FileStream log, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        lock (log)
        {
            if (log.Length + bytes.Length <= MaxLogBytes)
            {
                log.Write(bytes);
                log.Flush();
            }
        }
    }

    private void Finish(RunInfo run, int? exitCode)
    {
        int? total = null, passed = null, failed = null, skipped = null;
        var data = Path.Combine(FolderOf(run.Id), DataFile);
        try
        {
            if (File.Exists(data))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(data));
                var statuses = doc.RootElement.GetProperty("tests").EnumerateArray().Select(t => t.GetProperty("status").GetString()).ToList();
                total = statuses.Count;
                passed = statuses.Count(s => s == "passed");
                failed = statuses.Count(s => s == "failed");
                skipped = statuses.Count(s => s == "skipped");
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "data.json of run {RunId} could not be read", run.Id);
        }

        var status = total is null ? RunStatus.Error : failed > 0 || exitCode is not 0 ? RunStatus.Failed : RunStatus.Passed;
        var done = run with { Status = status, FinishedAt = DateTimeOffset.UtcNow, Total = total, Passed = passed, Failed = failed, Skipped = skipped, ExitCode = exitCode };

        TrySave(done);
        lock (_gate)
        {
            _current = done;
        }
    }

    private void Save(RunInfo run) =>
        File.WriteAllText(Path.Combine(FolderOf(run.Id), RunFile), JsonSerializer.Serialize(run, RunJson.Options));

    private void TrySave(RunInfo run)
    {
        try
        {
            Save(run);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "run.json of run {RunId} could not be written", run.Id);
        }
    }

    // 14 digits for runs created before 1.3.0, 17 (with milliseconds) since.
    [GeneratedRegex("^[0-9]{14}(?:[0-9]{3})?$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex CategoryPattern();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.+`]{0,400}$")]
    private static partial Regex TestNamePattern();
}
