using Microsoft.Playwright;

namespace E2eStarter.E2E.Pages;

public sealed class AutomatedTestsPage(IPage page)
{
    private ILocator Heading => page.GetByRole(AriaRole.Heading, new() { Name = "Automated tests" });
    private ILocator Rows => page.GetByRole(AriaRole.Row);

    public async Task<AutomatedTestsPage> OpenAsync()
    {
        await page.GotoAsync("/#/automated-tests");
        await Heading.WaitForAsync();
        return this;
    }

    public async Task FilterAsync(string category) =>
        await page.GetByRole(AriaRole.Button, new() { Name = category, Exact = true }).ClickAsync();

    public async Task<int> ResultRowCountAsync() => await Rows.CountAsync() - 1;
}
