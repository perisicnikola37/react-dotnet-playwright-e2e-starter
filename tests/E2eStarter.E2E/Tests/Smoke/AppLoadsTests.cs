using E2eStarter.E2E.Infrastructure;
using E2eStarter.E2E.Pages;
using E2eStarter.Playwright.Infrastructure;

namespace E2eStarter.E2E.Tests.Smoke;

[Trait(TestCategories.Key, TestCategories.Smoke)]
public sealed class AppLoadsTests(SuiteFixture suite) : AppTestBase(suite)
{
    [Fact]
    public async Task The_items_page_loads_with_its_heading()
    {
        var items = await new ItemsPage(Page).OpenAsync();

        (await items.HeadingTextAsync()).ShouldBe("Items");
    }

    [Fact]
    public async Task A_new_item_can_be_added_and_removed()
    {
        await Api.DeleteAllItemsAsync();
        var items = await new ItemsPage(Page).OpenAsync();

        await items.AddItemAsync("Smoke item");
        await items.WaitForItemAsync("Smoke item");
        (await items.ItemNamesAsync()).ShouldContain("Smoke item");

        await items.DeleteItemAsync("Smoke item");
        await items.WaitForEmptyStateAsync();
    }
}
