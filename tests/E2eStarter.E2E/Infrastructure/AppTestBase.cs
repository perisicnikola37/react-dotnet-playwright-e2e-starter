using E2eStarter.Playwright.Infrastructure;

namespace E2eStarter.E2E.Infrastructure;

[CollectionDefinition(UiTestBase.CollectionName)]
public sealed class BrowserCollection : ICollectionFixture<SuiteFixture>;

// Adds the application-specific API client on top of the reusable UiTestBase.
public abstract class AppTestBase(SuiteFixture suite) : UiTestBase(suite)
{
    // Disposed after the AfterTest cleanups ran, so those callbacks can still use it.
    protected ApiClient Api { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Api = new ApiClient(Settings);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Api.Dispose();
    }
}
