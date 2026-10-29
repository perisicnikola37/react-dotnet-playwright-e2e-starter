using System.Reflection;
using System.Text.Json;
using E2eStarter.Reporters;

if (args.Contains("--help") || args.Contains("-h") || args.Length == 0)
{
    Console.WriteLine("""
        e2e-report: turn a TRX report into a dashboard.

        Usage: e2e-report --trx <results.trx> --out <run folder> [options]

          --artifacts <dir>   failure evidence folder written by E2eStarter.Playwright
          --previous <file>   data.json of the previous run (new failure / still failing)
          --title <text>      dashboard title            (default "UI tests")
          --run-id <id>       run identifier             (default: current UTC time)
          --env <name>        environment label          (default "local")
          --base-url <url>    app under test             (default http://localhost:5180)

        Writes data.json, index.html (live dashboard) and report.html (standalone) to the run folder.
        """);
    return args.Length == 0 ? 64 : 0;
}

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var trx = Arg("--trx");
var outDir = Arg("--out");
if (trx is null || outDir is null || !File.Exists(trx))
{
    Console.Error.WriteLine("--trx must point to an existing file and --out is required.");
    return 64;
}

Directory.CreateDirectory(outDir);
var report = ReportBuilder.Build(new ReportOptions
{
    TrxPath = trx,
    OutputDirectory = outDir,
    ArtifactsDirectory = Arg("--artifacts"),
    PreviousDataPath = Arg("--previous"),
    Title = Arg("--title") ?? "UI tests",
    RunId = Arg("--run-id") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
    Environment = Arg("--env") ?? "local",
    BaseUrl = Arg("--base-url") ?? "http://localhost:5180",
});

var json = JsonSerializer.Serialize(report, ReportBuilder.Json);
await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("dashboard.html")!;
using var reader = new StreamReader(stream);
var template = await reader.ReadToEndAsync();

const string Placeholder = """<script id="report-data" type="application/json">null</script>""";
var embedded = $"""<script id="report-data" type="application/json">{json.Replace("</", "<\\/")}</script>""";

await File.WriteAllTextAsync(Path.Combine(outDir, "data.json"), json);
await File.WriteAllTextAsync(Path.Combine(outDir, "index.html"), template);
await File.WriteAllTextAsync(Path.Combine(outDir, "report.html"), template.Replace(Placeholder, embedded));

var failed = report.Tests.Count(t => t.Status == "failed");
Console.WriteLine($"{report.Tests.Count} tests, {failed} failed. Report written to {Path.GetFullPath(outDir)}");
return 0;
