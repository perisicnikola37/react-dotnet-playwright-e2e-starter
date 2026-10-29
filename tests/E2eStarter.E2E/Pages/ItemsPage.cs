using Microsoft.Playwright;

namespace E2eStarter.E2E.Pages;

// Page objects own the locators and expose intent-level methods. Tests never touch selectors.
public sealed class ItemsPage(IPage page)
{
    private ILocator Heading => page.GetByTestId("page-heading");
    private ILocator NameInput => page.GetByLabel("Item name");
    private ILocator AddButton => page.GetByRole(AriaRole.Button, new() { Name = "Add item" });
    private ILocator Rows => page.GetByTestId("item-row");
    private ILocator Error => page.GetByRole(AriaRole.Alert);
    private ILocator EmptyState => page.GetByTestId("empty-state");

    public async Task<ItemsPage> OpenAsync()
    {
        await page.GotoAsync("/");
        await Heading.WaitForAsync();
        // Either the list or the empty state means the first load finished.
        await page.Locator("[data-testid=item-list], [data-testid=empty-state]").First.WaitForAsync();
        return this;
    }

    public async Task<string> HeadingTextAsync() => (await Heading.InnerTextAsync()).Trim();

    public async Task AddItemAsync(string name)
    {
        await NameInput.FillAsync(name);
        await AddButton.ClickAsync();
    }

    public async Task DeleteItemAsync(string name) =>
        await page.GetByRole(AriaRole.Button, new() { Name = $"Delete {name}" }).ClickAsync();

    public async Task<IReadOnlyList<string>> ItemNamesAsync() =>
        (await Rows.GetByTestId("item-label").AllInnerTextsAsync()).ToList();

    public Task WaitForItemAsync(string name) =>
        Rows.Filter(new() { HasText = name }).First.WaitForAsync();

    public Task WaitForItemGoneAsync(string name) =>
        Rows.Filter(new() { HasText = name }).WaitForAsync(new() { State = WaitForSelectorState.Detached });

    public Task WaitForEmptyStateAsync() => EmptyState.WaitForAsync();

    public async Task<string> ErrorTextAsync()
    {
        await Error.WaitForAsync();
        return (await Error.InnerTextAsync()).Trim();
    }
}
