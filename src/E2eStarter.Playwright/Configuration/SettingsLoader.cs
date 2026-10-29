using Microsoft.Extensions.Configuration;

namespace E2eStarter.Playwright.Configuration;

public static class SettingsLoader
{
    // Precedence (lowest to highest): appsettings.json < appsettings.{E2E_ENVIRONMENT}.json < E2E_* variables.
    // Nested keys use a double underscore, e.g. E2E_Browser__Headless=false.
    public static TestSettings Load()
    {
        var environment = Environment.GetEnvironmentVariable("E2E_ENVIRONMENT");

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true);

        if (!string.IsNullOrWhiteSpace(environment))
        {
            builder.AddJsonFile($"appsettings.{environment}.json", optional: true);
        }

        return builder
            .AddEnvironmentVariables(prefix: "E2E_")
            .Build()
            .Get<TestSettings>() ?? new TestSettings();
    }
}
