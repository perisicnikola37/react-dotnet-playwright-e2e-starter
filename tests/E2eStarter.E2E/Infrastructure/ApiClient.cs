using System.Net.Http.Json;
using E2eStarter.Playwright.Configuration;

namespace E2eStarter.E2E.Infrastructure;

// Seeds and removes data through the API, so UI tests do not depend on each other and stay fast.
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ApiClient(TestSettings settings) => _http = new HttpClient { BaseAddress = new Uri(settings.ApiBaseUrl) };

    public async Task<Guid> CreateItemAsync(string name)
    {
        var response = await _http.PostAsJsonAsync("/api/items", new { name });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ItemDto>();
        return created!.Id;
    }

    public async Task DeleteItemAsync(Guid id) => await _http.DeleteAsync($"/api/items/{id}");

    public async Task<IReadOnlyList<ItemDto>> ListItemsAsync() =>
        await _http.GetFromJsonAsync<List<ItemDto>>("/api/items") ?? [];

    public async Task DeleteAllItemsAsync()
    {
        foreach (var item in await ListItemsAsync())
        {
            await DeleteItemAsync(item.Id);
        }
    }

    public void Dispose() => _http.Dispose();

    public sealed record ItemDto(Guid Id, string Name);
}
