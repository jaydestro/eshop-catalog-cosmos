using System.Text.Json.Serialization;
using eShop.Catalog.API.Model;

namespace eShop.Catalog.API.Infrastructure;

#nullable enable

/// <summary>
/// Cosmos persistence document for a <see cref="CatalogItem"/>. Wraps the public DTO with a
/// string <c>id</c> property as required by Cosmos DB. Partition key path is <c>/id</c>.
/// </summary>
internal sealed class CatalogItemDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Stored alongside Id so the document is self-describing if inspected directly.
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? PictureFileName { get; set; }
    public int CatalogTypeId { get; set; }
    public string? CatalogTypeName { get; set; }
    public int CatalogBrandId { get; set; }
    public string? CatalogBrandName { get; set; }
    public int AvailableStock { get; set; }
    public int RestockThreshold { get; set; }
    public int MaxStockThreshold { get; set; }
    public bool OnReorder { get; set; }

    public static CatalogItemDocument From(CatalogItem item) => new()
    {
        Id = item.Id.ToString(),
        ItemId = item.Id,
        Name = item.Name,
        Description = item.Description,
        Price = item.Price,
        PictureFileName = item.PictureFileName,
        CatalogTypeId = item.CatalogTypeId,
        CatalogTypeName = item.CatalogTypeName,
        CatalogBrandId = item.CatalogBrandId,
        CatalogBrandName = item.CatalogBrandName,
        AvailableStock = item.AvailableStock,
        RestockThreshold = item.RestockThreshold,
        MaxStockThreshold = item.MaxStockThreshold,
        OnReorder = item.OnReorder,
    };

    public CatalogItem ToCatalogItem() => new()
    {
        Id = ItemId != 0 ? ItemId : int.TryParse(Id, out var parsed) ? parsed : 0,
        Name = Name,
        Description = Description,
        Price = Price,
        PictureFileName = PictureFileName,
        CatalogTypeId = CatalogTypeId,
        CatalogTypeName = CatalogTypeName,
        CatalogBrandId = CatalogBrandId,
        CatalogBrandName = CatalogBrandName,
        AvailableStock = AvailableStock,
        RestockThreshold = RestockThreshold,
        MaxStockThreshold = MaxStockThreshold,
        OnReorder = OnReorder,
    };
}

internal sealed class CatalogBrandDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string Brand { get; set; } = string.Empty;

    public static CatalogBrandDocument From(CatalogBrand b) => new() { Id = b.Id.ToString(), BrandId = b.Id, Brand = b.Brand };
    public CatalogBrand ToBrand() => new(Brand) { Id = BrandId };
}

internal sealed class CatalogTypeDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    public int TypeId { get; set; }
    public string Type { get; set; } = string.Empty;

    public static CatalogTypeDocument From(CatalogType t) => new() { Id = t.Id.ToString(), TypeId = t.Id, Type = t.Type };
    public CatalogType ToType() => new(Type) { Id = TypeId };
}
