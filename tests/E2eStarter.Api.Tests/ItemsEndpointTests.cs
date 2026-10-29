using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace E2eStarter.Api.Tests;

public sealed class ItemsEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record ItemDto(Guid Id, string Name);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_an_item_returns_201_and_lists_it()
    {
        using var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/items", new { name = "First" }, Ct);
        var items = await client.GetFromJsonAsync<List<ItemDto>>("/api/items", Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        items!.ShouldContain(i => i.Name == "First");
    }

    [Fact]
    public async Task A_blank_name_is_rejected()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/items", new { name = "  " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_duplicate_name_returns_409()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/items", new { name = "Twin" }, Ct);

        var response = await client.PostAsJsonAsync("/api/items", new { name = "twin" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deleting_an_unknown_item_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/items/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
