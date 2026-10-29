using E2eStarter.E2E.Infrastructure;
using E2eStarter.E2E.Pages;
using E2eStarter.Playwright.Infrastructure;

namespace E2eStarter.E2E.Tests.Regression;

[Trait(TestCategories.Key, TestCategories.Regression)]
public sealed class ItemsTests(SuiteFixture suite) : AppTestBase(suite)
{
    // Names are unique per test, so a leftover from a crashed run never collides with a later one.
    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..28];

    [Fact]
    public async Task Items_created_through_the_API_are_listed_on_the_page()
    {
        var name = UniqueName("Seeded");
        var id = await Api.CreateItemAsync(name);
        AfterTest(() => Api.DeleteItemAsync(id));

        var items = await new ItemsPage(Page).OpenAsync();

        await items.WaitForItemAsync(name);
    }

    [Fact]
    public async Task Adding_an_item_with_a_blank_name_shows_a_validation_error()
    {
        var items = await new ItemsPage(Page).OpenAsync();

        await items.AddItemAsync("   ");

        (await items.ErrorTextAsync()).ShouldContain("Name is required");
    }

    [Fact]
    public async Task Adding_a_duplicate_name_is_rejected()
    {
        Step("Seed an item through the API");
        var name = UniqueName("Duplicate");
        var id = await Api.CreateItemAsync(name);
        AfterTest(() => Api.DeleteItemAsync(id));

        Step("Open the items page");
        var items = await new ItemsPage(Page).OpenAsync();

        Step("Add an item with the same name");
        await items.AddItemAsync(name);

        Step("See the duplicate error");
        (await items.ErrorTextAsync()).ShouldContain("already exists");
    }

    [Fact]
    public async Task A_deleted_item_stays_gone_after_a_reload()
    {
        var name = UniqueName("Removable");
        await Api.CreateItemAsync(name);
        var items = await new ItemsPage(Page).OpenAsync();

        await items.DeleteItemAsync(name);
        await items.WaitForItemGoneAsync(name);
        await Page.ReloadAsync();
        await items.OpenAsync();

        (await items.ItemNamesAsync()).ShouldNotContain(name);
    }
}
