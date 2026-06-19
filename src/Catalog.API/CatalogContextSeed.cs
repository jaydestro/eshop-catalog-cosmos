using Microsoft.Azure.Cosmos;
using System.Text.Json;

namespace eShop.Catalog.API.Infrastructure;

#nullable enable

/// <summary>Seeds the Cosmos catalog from <c>Setup/catalog.json</c> on first startup.</summary>
public sealed class CatalogContextSeed
{
    private readonly CatalogContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CatalogContextSeed> _logger;

    public CatalogContextSeed(CatalogContext context, IWebHostEnvironment env, ILogger<CatalogContextSeed> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Skip if there's already at least one item.
        var existingCounts = new List<long>();
        using (var iter = _context.Items.GetItemQueryIterator<long>(new Microsoft.Azure.Cosmos.QueryDefinition("SELECT VALUE COUNT(1) FROM c")))
        {
            while (iter.HasMoreResults) existingCounts.AddRange(await iter.ReadNextAsync());
        }
        if (existingCounts.FirstOrDefault() > 0)
        {
            _logger.LogInformation("Catalog already seeded ({Count} items)", existingCounts[0]);
            return;
        }

        var sourcePath = Path.Combine(_env.ContentRootPath, "Setup", "catalog.json");
        var sourceJson = await File.ReadAllTextAsync(sourcePath);
        var entries = JsonSerializer.Deserialize<CatalogSourceEntry[]>(sourceJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? Array.Empty<CatalogSourceEntry>();

        // Build brand and type lookup with assigned ids.
        var brandIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var nextBrandId = 1;
        foreach (var brand in entries.Select(e => e.Brand!).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            brandIds[brand] = nextBrandId++;
        }
        var nextTypeId = 1;
        foreach (var type in entries.Select(e => e.Type!).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            typeIds[type] = nextTypeId++;
        }

        foreach (var (name, id) in brandIds)
        {
            var doc = Infrastructure.CatalogBrandDocument.From(new Model.CatalogBrand(name) { Id = id });
            await _context.Brands.UpsertItemAsync(doc, new PartitionKey(doc.Id));
        }
        foreach (var (name, id) in typeIds)
        {
            var doc = Infrastructure.CatalogTypeDocument.From(new Model.CatalogType(name) { Id = id });
            await _context.Types.UpsertItemAsync(doc, new PartitionKey(doc.Id));
        }
        _logger.LogInformation("Seeded {NumBrands} brands, {NumTypes} types", brandIds.Count, typeIds.Count);

        var items = entries.Where(e => !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.Brand) && !string.IsNullOrWhiteSpace(e.Type));
        var inserted = 0;
        foreach (var src in items)
        {
            var item = new Model.CatalogItem(src.Name!)
            {
                Id = src.Id,
                Description = src.Description,
                Price = src.Price,
                CatalogBrandId = brandIds[src.Brand!],
                CatalogBrandName = src.Brand,
                CatalogTypeId = typeIds[src.Type!],
                CatalogTypeName = src.Type,
                AvailableStock = 100,
                MaxStockThreshold = 200,
                RestockThreshold = 10,
                PictureFileName = $"{src.Id}.webp",
            };
            await _context.UpsertItemAsync(item);
            inserted++;
        }
        _logger.LogInformation("Seeded {NumItems} catalog items", inserted);
    }

    private sealed class CatalogSourceEntry
    {
        public int Id { get; set; }
        public string? Type { get; set; }
        public string? Brand { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
    }
}
