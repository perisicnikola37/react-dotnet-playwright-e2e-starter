using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace E2eStarter.Reporters;

public sealed record TestEntry(
    string Name,
    string[] Categories,
    string Status,
    long DurationMs,
    string? Previous,
    string? Error,
    string? Stack,
    string? Url,
    string? Screenshot,
    string? PageHtml,
    string? Trace,
    string[]? Console = null,
    string[]? Steps = null,
    int? FailedStep = null);

public sealed record RunReport(
    string Title,
    string RunId,
    string? PreviousRunId,
    string Environment,
    string BaseUrl,
    DateTimeOffset StartedAt,
    long ElapsedMs,
    IReadOnlyList<TestEntry> Tests);

public sealed class ReportOptions
{
    public required string TrxPath { get; init; }

    public required string OutputDirectory { get; init; }

    // Folder with one sub-folder per failed test (screenshot.png, page.html, url.txt, trace.zip, console.json, steps.json),
    // as written by E2eStarter.Playwright. Evidence inside OutputDirectory is referenced in place, anything else is copied.
    public string? ArtifactsDirectory { get; init; }

    // data.json of the previous run; enables "new failure" and "still failing".
    public string? PreviousDataPath { get; init; }

    public string Title { get; init; } = "UI tests";

    public string RunId { get; init; } = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

    public string Environment { get; init; } = "local";

    public string BaseUrl { get; init; } = "http://localhost:5180";
}

// File names written by UiTestBase (E2eStarter.Playwright.Infrastructure.RunArtifacts).
public static class EvidenceFiles
{
    public const string Screenshot = "screenshot.png";
    public const string PageHtml = "page.html";
    public const string Url = "url.txt";
    public const string Trace = "trace.zip";
    public const string Console = "console.json";
    public const string Steps = "steps.json";
}

internal sealed record Evidence(
    string? Url = null,
    string? Screenshot = null,
    string? PageHtml = null,
    string? Trace = null,
    string[]? Console = null,
    string[]? Steps = null);

public static class ReportBuilder
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static RunReport Build(ReportOptions options)
    {
        var doc = XDocument.Load(options.TrxPath);
        var categories = doc.Descendants(Ns + "UnitTest")
            .ToDictionary(
                u => (string)u.Attribute("id")!,
                u => u.Descendants(Ns + "TestCategoryItem").Select(c => (string)c.Attribute("TestCategory")!).Distinct().ToArray());

        var previous = LoadPrevious(options.PreviousDataPath);
        var tests = new List<TestEntry>();

        foreach (var result in doc.Descendants(Ns + "UnitTestResult"))
        {
            var name = (string)result.Attribute("testName")!;
            var status = ((string?)result.Attribute("outcome")) switch
            {
                "Passed" => "passed",
                "Failed" => "failed",
                _ => "skipped",
            };
            var error = result.Descendants(Ns + "ErrorInfo").FirstOrDefault();
            var evidence = CollectEvidence(options, name);
            var steps = status == "failed" ? evidence.Steps : null;

            tests.Add(new TestEntry(
                name,
                categories.GetValueOrDefault((string)result.Attribute("testId")!) ?? [],
                status,
                (long)TimeSpan.Parse((string?)result.Attribute("duration") ?? "0:0:0", CultureInfo.InvariantCulture).TotalMilliseconds,
                previous.Tests.GetValueOrDefault(name),
                error?.Element(Ns + "Message")?.Value,
                error?.Element(Ns + "StackTrace")?.Value,
                evidence.Url,
                evidence.Screenshot,
                evidence.PageHtml,
                evidence.Trace,
                evidence.Console,
                steps,
                steps is { Length: > 0 } ? steps.Length - 1 : null));
        }

        var (start, finish) = RunTimes(doc);

        return new RunReport(
            options.Title,
            options.RunId,
            previous.RunId,
            options.Environment,
            options.BaseUrl,
            start,
            (long)(finish - start).TotalMilliseconds,
            tests.OrderBy(t => t.Name, StringComparer.Ordinal).ToList());
    }

    // TRX files from some runners have no <Times>; fall back to the first start and last end of the results.
    private static (DateTimeOffset Start, DateTimeOffset Finish) RunTimes(XDocument doc)
    {
        DateTimeOffset? Parse(XElement? element, string attribute) =>
            DateTimeOffset.TryParse((string?)element?.Attribute(attribute), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

        var times = doc.Descendants(Ns + "Times").FirstOrDefault();
        var results = doc.Descendants(Ns + "UnitTestResult").ToList();
        var start = Parse(times, "start") ?? results.Select(r => Parse(r, "startTime")).Min() ?? DateTimeOffset.UtcNow;
        var finish = Parse(times, "finish") ?? results.Select(r => Parse(r, "endTime")).Max() ?? start;
        return (start, finish < start ? start : finish);
    }

    // Returns the evidence of a failed test with paths relative to the run folder, copying it there when needed.
    private static Evidence CollectEvidence(ReportOptions options, string testName)
    {
        if (options.ArtifactsDirectory is null)
        {
            return new Evidence();
        }

        var folderName = SanitizeFileName(testName);
        var source = Path.GetFullPath(Path.Combine(options.ArtifactsDirectory, folderName));
        if (!Directory.Exists(source))
        {
            return new Evidence();
        }

        var output = Path.GetFullPath(options.OutputDirectory) + Path.DirectorySeparatorChar;
        var target = source.StartsWith(output, StringComparison.Ordinal) ? source : Path.Combine(output, "evidence", folderName);
        Directory.CreateDirectory(target);

        string? Link(string file)
        {
            var from = Path.Combine(source, file);
            if (!File.Exists(from))
            {
                return null;
            }

            var to = Path.Combine(target, file);
            if (to != from)
            {
                File.Copy(from, to, overwrite: true);
            }

            return string.Join('/', Path.GetRelativePath(output, to).Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
        }

        string? Text(string file) => File.Exists(Path.Combine(source, file)) ? File.ReadAllText(Path.Combine(source, file)).Trim() : null;

        string[]? List(string file)
        {
            try
            {
                return Text(file) is { } json ? JsonSerializer.Deserialize<string[]>(json) : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return new Evidence(Text(EvidenceFiles.Url), Link(EvidenceFiles.Screenshot), Link(EvidenceFiles.PageHtml), Link(EvidenceFiles.Trace), List(EvidenceFiles.Console), List(EvidenceFiles.Steps));
    }

    private static (string? RunId, Dictionary<string, string> Tests) LoadPrevious(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return (null, []);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var tests = root.GetProperty("tests").EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("status").GetString()!);
        return (root.TryGetProperty("runId", out var id) ? id.GetString() : null, tests);
    }

    // Must match RunArtifacts.SanitizeFileName in E2eStarter.Playwright.
    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return cleaned.Length > 100 ? cleaned[..100] : cleaned;
    }
}
