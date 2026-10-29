using E2eStarter.E2E.Infrastructure;
using E2eStarter.E2E.Pages;
using E2eStarter.Playwright.Infrastructure;

namespace E2eStarter.E2E.Tests.Regression;

[Trait(TestCategories.Key, TestCategories.Regression)]
public sealed class AutomatedTestsPageTests(SuiteFixture suite) : AppTestBase(suite)
{
    [Fact]
    public async Task The_category_filter_narrows_the_results()
    {
        var dashboard = await new AutomatedTestsPage(Page).OpenAsync();
        var all = await dashboard.ResultRowCountAsync();

        await dashboard.FilterAsync("Smoke");

        (await dashboard.ResultRowCountAsync()).ShouldBeLessThan(all);
    }
}
