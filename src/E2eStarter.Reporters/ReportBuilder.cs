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
    string? Trace);

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

    // Folder with one sub-folder per failed test (screenshot.png, page.html, url.txt, trace.zip), as written by E2eStarter.Playwright.
    public string? ArtifactsDirectory { get; init; }

    // data.json of the previous run; enables "new failure" and "still failing".
    public string? PreviousDataPath { get; init; }

    public string Title { get; init; } = "UI tests";

    public string RunId { get; init; } = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

    public string Environment { get; init; } = "local";

    public string BaseUrl { get; init; } = "http://localhost:5180";
}

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

            tests.Add(new TestEntry(
                name,
                categories.GetValueOrDefault((string)result.Attribute("testId")!) ?? [],
                status,
                (long)TimeSpan.Parse((string?)result.Attribute("duration") ?? "0:0:0", System.Globalization.CultureInfo.InvariantCulture).TotalMilliseconds,
                previous.Tests.GetValueOrDefault(name),
                error?.Element(Ns + "Message")?.Value,
                error?.Element(Ns + "StackTrace")?.Value,
                evidence.Url,
                evidence.Screenshot,
                evidence.PageHtml,
                evidence.Trace));
        }

        var times = doc.Descendants(Ns + "Times").First();
        var start = DateTimeOffset.Parse((string)times.Attribute("start")!, System.Globalization.CultureInfo.InvariantCulture);
        var finish = DateTimeOffset.Parse((string)times.Attribute("finish")!, System.Globalization.CultureInfo.InvariantCulture);

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

    // Copies the evidence of a failed test next to the report and returns paths relative to the run folder.
    private static (string? Url, string? Screenshot, string? PageHtml, string? Trace) CollectEvidence(ReportOptions options, string testName)
    {
        if (options.ArtifactsDirectory is null)
        {
            return default;
        }

        var source = Path.Combine(options.ArtifactsDirectory, SanitizeFileName(testName));
        if (!Directory.Exists(source))
        {
            return default;
        }

        var target = Path.Combine(options.OutputDirectory, "evidence", SanitizeFileName(testName));
        Directory.CreateDirectory(target);

        string? Copy(string file)
        {
            var from = Path.Combine(source, file);
            if (!File.Exists(from))
            {
                return null;
            }

            File.Copy(from, Path.Combine(target, file), overwrite: true);
            return $"evidence/{Uri.EscapeDataString(SanitizeFileName(testName))}/{file}";
        }

        var urlFile = Path.Combine(source, "url.txt");
        return (File.Exists(urlFile) ? File.ReadAllText(urlFile).Trim() : null, Copy("screenshot.png"), Copy("page.html"), Copy("trace.zip"));
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

    // Must match the folder naming of UiTestBase in E2eStarter.Playwright.
    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return cleaned.Length > 100 ? cleaned[..100] : cleaned;
    }
}
