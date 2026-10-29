using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using E2eStarter.Playwright.Configuration;
using E2eStarter.Playwright.Infrastructure;
using Xunit.Sdk;
using Xunit.v3;

namespace E2eStarter.Playwright.Reporting;

// Streams run progress to TestSettings.LiveEventsPath (events.jsonl) so the dashboard can show a run while it is going.
// Register it once in the test project:  [assembly: TestFramework(typeof(LiveProgressTestFramework))]
// Without LiveEventsPath (E2E_LiveEventsPath) it behaves exactly like the default xUnit v3 framework.
public sealed class LiveProgressTestFramework : XunitTestFramework
{
    public LiveProgressTestFramework()
    {
    }

    public LiveProgressTestFramework(string? configFileName)
        : base(configFileName)
    {
    }

    protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly)
    {
        var executor = base.CreateExecutor(assembly);
        var settings = SettingsLoader.Load();
        return string.IsNullOrWhiteSpace(settings.LiveEventsPath) ? executor : new LiveProgressExecutor(executor, settings);
    }

    private sealed class LiveProgressExecutor(ITestFrameworkExecutor inner, TestSettings settings) : ITestFrameworkExecutor
    {
        public async ValueTask RunTestCases(
            IReadOnlyCollection<ITestCase> testCases,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions,
            CancellationToken? cancellationToken)
        {
            using var events = new LiveEventWriter(settings);
            events.RunStart(testCases);
            try
            {
                await inner.RunTestCases(testCases, new LiveProgressSink(executionMessageSink, events), executionOptions, cancellationToken);
            }
            finally
            {
                events.RunEnd();
            }
        }
    }

    private sealed class LiveProgressSink(IMessageSink inner, LiveEventWriter events) : IMessageSink
    {
        private readonly ConcurrentDictionary<string, (string Name, string[] Categories)> _started = new();
        private readonly ConcurrentDictionary<string, (string Status, string? Error, string? Stack)> _results = new();

        public bool OnMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestStarting starting:
                    _started[starting.TestUniqueID] = (starting.TestDisplayName, LiveEventWriter.Categories(starting.Traits));
                    events.TestStart(starting.TestDisplayName);
                    break;
                case ITestFailed failed:
                    _results[failed.TestUniqueID] = ("failed", string.Join(Environment.NewLine, failed.Messages), string.Join(Environment.NewLine, failed.StackTraces));
                    break;
                case ITestSkipped skipped:
                    _results[skipped.TestUniqueID] = ("skipped", skipped.Reason, null);
                    break;
                case ITestNotRun notRun:
                    _results[notRun.TestUniqueID] = ("skipped", null, null);
                    break;
                case ITestPassed passed:
                    _results[passed.TestUniqueID] = ("passed", null, null);
                    break;

                // Finished arrives after the test class was disposed, so UiTestBase has written the evidence by now.
                case ITestFinished finished when _started.TryRemove(finished.TestUniqueID, out var test):
                    _results.TryRemove(finished.TestUniqueID, out var result);
                    events.TestEnd(test.Name, test.Categories, result.Status ?? "failed", finished.ExecutionTime, result.Error, result.Stack);
                    break;
            }

            return inner.OnMessage(message);
        }
    }
}

// Contract with the dashboard: one camelCase JSON object per line.
//   run-start  { runId, title, environment, baseUrl, startedAt, tests: [{ name, categories }] }
//   test-start { name }
//   test-end   { test: <same shape as an entry of data.json "tests"> }
//   run-end    { finishedAt }
internal sealed class LiveEventWriter : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TestSettings _settings;
    private readonly string _directory;
    private readonly StreamWriter _writer;
    private readonly Lock _gate = new();

    public LiveEventWriter(TestSettings settings)
    {
        _settings = settings;
        var path = Path.GetFullPath(settings.LiveEventsPath!);
        _directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(_directory);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
    }

    public static string[] Categories(IReadOnlyDictionary<string, IReadOnlyCollection<string>> traits) =>
        traits.TryGetValue(TestCategories.Key, out var values) ? [.. values] : [];

    public void RunStart(IReadOnlyCollection<ITestCase> testCases) => Write(new
    {
        type = "run-start",
        runId = Path.GetFileName(_directory),
        title = _settings.Title,
        environment = _settings.Environment,
        baseUrl = _settings.BaseUrl,
        startedAt = DateTimeOffset.UtcNow,
        tests = testCases.Select(t => new { name = t.TestCaseDisplayName, categories = Categories(t.Traits) }),
    });

    public void TestStart(string name) => Write(new { type = "test-start", name });

    public void TestEnd(string name, string[] categories, string status, decimal seconds, string? error, string? stack)
    {
        var folder = RunArtifacts.FolderFor(_settings, name);
        string At(string file) => Path.Combine(folder, file);
        string? Evidence(string file) =>
            File.Exists(At(file)) ? UrlPath(Path.GetRelativePath(_directory, At(file))) : null;
        string[]? ReadList(string file) =>
            File.Exists(At(file)) ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(At(file))) : null;

        var steps = ReadList(RunArtifacts.Steps);
        Write(new
        {
            type = "test-end",
            test = new
            {
                name,
                categories,
                status,
                durationMs = (long)(seconds * 1000),
                error = string.IsNullOrEmpty(error) ? null : error,
                stack = string.IsNullOrEmpty(stack) ? null : stack,
                url = File.Exists(At(RunArtifacts.Url)) ? File.ReadAllText(At(RunArtifacts.Url)).Trim() : null,
                screenshot = Evidence(RunArtifacts.Screenshot),
                pageHtml = Evidence(RunArtifacts.PageHtml),
                trace = Evidence(RunArtifacts.Trace),
                console = ReadList(RunArtifacts.Console),
                steps,
                failedStep = steps is { Length: > 0 } && status == "failed" ? steps.Length - 1 : (int?)null,
            },
        });
    }

    // Relative URL for the dashboard; test names can contain characters such as '#' or '%'.
    private static string UrlPath(string relative) =>
        string.Join('/', relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Select(Uri.EscapeDataString));

    public void RunEnd() => Write(new { type = "run-end", finishedAt = DateTimeOffset.UtcNow });

    public void Dispose() => _writer.Dispose();

    private void Write(object payload)
    {
        var line = JsonSerializer.Serialize(payload, Json);
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }
}
