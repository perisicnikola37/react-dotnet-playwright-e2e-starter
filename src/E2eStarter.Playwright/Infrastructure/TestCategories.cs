namespace E2eStarter.Playwright.Infrastructure;

public static class TestCategories
{
    public const string Key = "Category";

    // Critical path; run after every deployment.
    public const string Smoke = "Smoke";

    // Broader journeys; run nightly or before a release.
    public const string Regression = "Regression";
}
