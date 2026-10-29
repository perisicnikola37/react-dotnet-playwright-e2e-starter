using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore.Tests;

public sealed class DashboardTokensTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void A_token_is_valid_until_it_expires()
    {
        var clock = new Clock();
        var tokens = new DashboardTokens(Options.Create(new AutomatedTestsOptions { DashboardTokenLifetime = TimeSpan.FromMinutes(5) }), clock);

        var token = tokens.Issue();
        var validNow = tokens.IsValid(token.Token);
        clock.Now += TimeSpan.FromMinutes(6);

        validNow.ShouldBeTrue();
        tokens.IsValid(token.Token).ShouldBeFalse();
        tokens.IsValid(null).ShouldBeFalse();
    }
}
