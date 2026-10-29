using E2eStarter.Playwright.Configuration;

namespace E2eStarter.Playwright.Infrastructure;

// Where failure evidence goes: {Artifacts:Directory}/{E2E_RUN_ID}/{test}/. Shared by UiTestBase and the live progress
// reporter, so both resolve the same folder for a test. The root is fixed once per process.
public static class RunArtifacts
{
    public const string Screenshot = "screenshot.png";
    public const string PageHtml = "page.html";
    public const string Url = "url.txt";
    public const string Trace = "trace.zip";
    public const string Console = "console.json";
    public const string Steps = "steps.json";

    private static readonly Lock Gate = new();
    private static string? _root;

    public static string Root(TestSettings settings)
    {
        lock (Gate)
        {
            return _root ??= Path.GetFullPath(Path.Combine(
                settings.Artifacts.Directory,
                Environment.GetEnvironmentVariable("E2E_RUN_ID") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        }
    }

    public static string FolderFor(TestSettings settings, string testDisplayName) =>
        Path.Combine(Root(settings), SanitizeFileName(testDisplayName));

    // E2eStarter.Reporters uses the same rule to find the folder of a test.
    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return cleaned.Length > 100 ? cleaned[..100] : cleaned;
    }
}
