namespace E2eStarter.Reporters.Tests;

public sealed class ReportBuilderTests : IDisposable
{
    private const string Failing = "App.Tests.Items.Adding_fails";

    private readonly string _run = Directory.CreateTempSubdirectory("e2e-report-tests-").FullName;

    public void Dispose() => Directory.Delete(_run, recursive: true);

    private string WriteTrx(bool withTimes = true)
    {
        var times = withTimes ? """<Times start="2026-01-01T10:00:00Z" finish="2026-01-01T10:00:10Z" />""" : "";
        var path = Path.Combine(_run, "results.trx");
        File.WriteAllText(path, $"""
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              {times}
              <Results>
                <UnitTestResult testId="1" testName="{Failing}" outcome="Failed" duration="00:00:01.5000000"
                    startTime="2026-01-01T10:00:01Z" endTime="2026-01-01T10:00:02.5Z">
                  <Output><ErrorInfo><Message>boom</Message><StackTrace>at Items.cs:line 1</StackTrace></ErrorInfo></Output>
                </UnitTestResult>
                <UnitTestResult testId="2" testName="App.Tests.Items.Listing_works" outcome="Passed" duration="00:00:00.2500000"
                    startTime="2026-01-01T10:00:00Z" endTime="2026-01-01T10:00:04Z" />
              </Results>
              <TestDefinitions>
                <UnitTest id="1" name="{Failing}"><TestCategory><TestCategoryItem TestCategory="Smoke" /></TestCategory></UnitTest>
                <UnitTest id="2" name="App.Tests.Items.Listing_works" />
              </TestDefinitions>
            </TestRun>
            """);
        return path;
    }

    private static void WriteEvidence(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, EvidenceFiles.Screenshot), "png");
        File.WriteAllText(Path.Combine(folder, EvidenceFiles.Url), "http://app.test/items\n");
        File.WriteAllText(Path.Combine(folder, EvidenceFiles.Console), """["[error] 500"]""");
        File.WriteAllText(Path.Combine(folder, EvidenceFiles.Steps), """["Open","Add","Check"]""");
    }

    [Fact]
    public void Results_categories_and_durations_come_from_the_TRX()
    {
        var report = ReportBuilder.Build(new ReportOptions { TrxPath = WriteTrx(), OutputDirectory = _run });

        report.ElapsedMs.ShouldBe(10_000);
        report.Tests.Select(t => (t.Name, t.Status, t.DurationMs)).ShouldBe([(Failing, "failed", 1500), ("App.Tests.Items.Listing_works", "passed", 250)]);
        report.Tests[0].Categories.ShouldBe(["Smoke"]);
        report.Tests[0].Error.ShouldBe("boom");
    }

    [Fact]
    public void Without_Times_the_run_spans_its_results()
    {
        var report = ReportBuilder.Build(new ReportOptions { TrxPath = WriteTrx(withTimes: false), OutputDirectory = _run });

        report.StartedAt.ShouldBe(DateTimeOffset.Parse("2026-01-01T10:00:00Z"));
        report.ElapsedMs.ShouldBe(4_000);
    }

    [Fact]
    public void Evidence_inside_the_run_folder_is_referenced_in_place_with_console_and_steps()
    {
        var evidence = Path.Combine(_run, "evidence");
        WriteEvidence(Path.Combine(evidence, ReportBuilder.SanitizeFileName(Failing)));

        var test = ReportBuilder.Build(new ReportOptions { TrxPath = WriteTrx(), OutputDirectory = _run, ArtifactsDirectory = evidence }).Tests[0];

        test.Screenshot.ShouldBe($"evidence/{Failing}/screenshot.png");
        test.Url.ShouldBe("http://app.test/items");
        test.Console.ShouldBe(["[error] 500"]);
        test.Steps.ShouldBe(["Open", "Add", "Check"]);
        test.FailedStep.ShouldBe(2);
        Directory.GetDirectories(evidence).Length.ShouldBe(1);
    }

    [Fact]
    public void Evidence_from_elsewhere_is_copied_next_to_the_report()
    {
        var outside = Directory.CreateTempSubdirectory("e2e-report-evidence-").FullName;
        try
        {
            WriteEvidence(Path.Combine(outside, ReportBuilder.SanitizeFileName(Failing)));

            var test = ReportBuilder.Build(new ReportOptions { TrxPath = WriteTrx(), OutputDirectory = _run, ArtifactsDirectory = outside }).Tests[0];

            test.Screenshot.ShouldBe($"evidence/{Failing}/screenshot.png");
            File.Exists(Path.Combine(_run, "evidence", Failing, "screenshot.png")).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void The_previous_run_marks_new_and_repeated_failures()
    {
        var previous = Path.Combine(_run, "previous.json");
        File.WriteAllText(previous, $$"""{"runId":"20251231000000","tests":[{"name":"{{Failing}}","status":"passed"}]}""");

        var report = ReportBuilder.Build(new ReportOptions { TrxPath = WriteTrx(), OutputDirectory = _run, PreviousDataPath = previous });

        report.PreviousRunId.ShouldBe("20251231000000");
        report.Tests[0].Previous.ShouldBe("passed");
        report.Tests[1].Previous.ShouldBeNull();
    }
}
