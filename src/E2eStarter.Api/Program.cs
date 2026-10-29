using System.Collections.Concurrent;
using E2eStarter.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ItemStore>();
builder.Services.AddHealthChecks();
builder.Services.AddAutomatedTests(o =>
{
    o.Enabled = !builder.Environment.IsProduction();
    o.WorkingDirectory = Path.GetFullPath(builder.Configuration["AutomatedTests:WorkingDirectory"] ?? ".", builder.Environment.ContentRootPath);
    o.Environment["AUTOMATED_TESTS_TITLE"] = "Items app · E2E tests";
    o.Environment["AUTOMATED_TESTS_ENV"] = builder.Environment.EnvironmentName;
});

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapAutomatedTests();

var items = app.MapGroup("/api/items");

items.MapGet("/", (ItemStore store) => store.All());

items.MapPost("/", (CreateItem request, ItemStore store) =>
{
    var name = request.Name?.Trim();
    if (string.IsNullOrEmpty(name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
    }

    if (store.Exists(name))
    {
        return Results.Conflict(new { error = $"An item named '{name}' already exists." });
    }

    var item = store.Add(name);
    return Results.Created($"/api/items/{item.Id}", item);
});

items.MapDelete("/{id:guid}", (Guid id, ItemStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound());

app.Run();

public sealed record Item(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record CreateItem(string? Name);

// Makes the entry point visible to WebApplicationFactory in the API tests.
public partial class Program;

public sealed class ItemStore
{
    private readonly ConcurrentDictionary<Guid, Item> _items = new();

    public IReadOnlyList<Item> All() => _items.Values.OrderBy(i => i.CreatedAt).ToList();

    public bool Exists(string name) =>
        _items.Values.Any(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

    public Item Add(string name)
    {
        var item = new Item(Guid.NewGuid(), name, DateTimeOffset.UtcNow);
        _items[item.Id] = item;
        return item;
    }

    public bool Remove(Guid id) => _items.TryRemove(id, out _);
}
