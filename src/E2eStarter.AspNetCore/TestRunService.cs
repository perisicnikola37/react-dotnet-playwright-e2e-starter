using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore;

public sealed partial class TestRunService(IOptions<AutomatedTestsOptions> options)
{
    private const long MaxLogBytes = 1_000_000;

    private readonly AutomatedTestsOptions _options = options.Value;
    private readonly object _gate = new();
    private RunInfo? _current;

    public string RunsRoot => Path.GetFullPath(_options.RunsDirectory, _options.WorkingDirectory);

    public static bool IsValidId(string id) => IdPattern().IsMatch(id);

    public string FolderOf(string id) => Path.Combine(RunsRoot, id);

    // Returns null while another run is in progress (only one run at a time, tests share the app under test).
    public RunInfo? Start(string? category)
    {
        if (category is not null && !CategoryPattern().IsMatch(category))
        {
            throw new ArgumentException("Category may only contain letters, digits, '-' and '_'.", nameof(category));
        }

        lock (_gate)
        {
            if (_current is { Status: RunStatus.Running })
            {
                return null;
            }

            var id = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            Directory.CreateDirectory(FolderOf(id));
            _current = new RunInfo(id, RunStatus.Running, category, DateTimeOffset.UtcNow, null, null, null, null, null, null);
            Save(_current);
        }

        var run = _current;
        _ = Task.Run(() => ExecuteAsync(run));
        return run;
    }

    public IReadOnlyList<RunInfo> List()
    {
        if (!Directory.Exists(RunsRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(RunsRoot)
            .Select(d => Get(Path.GetFileName(d)))
            .OfType<RunInfo>()
            .OrderByDescending(r => r.Id, StringComparer.Ordinal)
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

        var file = Path.Combine(FolderOf(id), "run.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<RunInfo>(File.ReadAllText(file), RunJson.Options) : null;
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

        foreach (var (key, value) in _options.Environment)
        {
            info.Environment[key] = value;
        }

        var previous = List().FirstOrDefault(r => r.Id != run.Id && r.Status is RunStatus.Passed or RunStatus.Failed);
        if (previous is not null)
        {
            info.Environment["E2E_PREVIOUS_DATA"] = Path.Combine(FolderOf(previous.Id), "data.json");
        }

        int? exitCode = null;
        try
        {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("The test command did not start.");
            using var timeout = new CancellationTokenSource(_options.Timeout);
            await using var log = File.Create(Path.Combine(folder, "runner.log"));

            var copy = Task.WhenAll(Pump(process.StandardOutput, log), Pump(process.StandardError, log));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await copy;
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
            }

            exitCode = process.HasExited ? process.ExitCode : null;
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "runner.log"), ex.ToString());
        }

        Finish(run, exitCode);
    }

    private static async Task Pump(StreamReader reader, FileStream log)
    {
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            if (log.Length > MaxLogBytes)
            {
                continue;
            }

            lock (log)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(buffer, 0, read);
                log.Write(bytes);
            }
        }
    }

    private void Finish(RunInfo run, int? exitCode)
    {
        int? total = null, passed = null, failed = null, skipped = null;
        var data = Path.Combine(FolderOf(run.Id), "data.json");
        if (File.Exists(data))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(data));
            var statuses = doc.RootElement.GetProperty("tests").EnumerateArray().Select(t => t.GetProperty("status").GetString()).ToList();
            total = statuses.Count;
            passed = statuses.Count(s => s == "passed");
            failed = statuses.Count(s => s == "failed");
            skipped = statuses.Count(s => s == "skipped");
        }

        var status = total is null ? RunStatus.Error : failed > 0 || exitCode is not 0 ? RunStatus.Failed : RunStatus.Passed;
        var done = run with { Status = status, FinishedAt = DateTimeOffset.UtcNow, Total = total, Passed = passed, Failed = failed, Skipped = skipped, ExitCode = exitCode };

        lock (_gate)
        {
            _current = done;
        }

        Save(done);
    }

    private void Save(RunInfo run) =>
        File.WriteAllText(Path.Combine(FolderOf(run.Id), "run.json"), JsonSerializer.Serialize(run, RunJson.Options));

    [GeneratedRegex("^[0-9]{14}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex CategoryPattern();
}
