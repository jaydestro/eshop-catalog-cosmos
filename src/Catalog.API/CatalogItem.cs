using System.Text.Json.Serialization;

namespace eShop.Catalog.API.Model;

#nullable enable

/// <summary>
/// Public catalog item shape used by the HTTP API and WebApp client. Stays an int-id record so
/// existing consumers (WebApp's <c>CatalogItem</c> record) keep working unchanged.
/// </summary>
public sealed class CatalogItem
{
    public int Id { get; set; }
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

    [JsonIgnore]
    public CatalogBrand? CatalogBrand =>
        CatalogBrandId == 0 ? null : new CatalogBrand(CatalogBrandName ?? string.Empty) { Id = CatalogBrandId };

    [JsonIgnore]
    public CatalogType? CatalogType =>
        CatalogTypeId == 0 ? null : new CatalogType(CatalogTypeName ?? string.Empty) { Id = CatalogTypeId };

    public CatalogItem() { }
    public CatalogItem(string name) { Name = name; }

    public int RemoveStock(int quantityDesired)
    {
        if (AvailableStock == 0)
        {
            throw new eShop.Catalog.API.Infrastructure.Exceptions.CatalogDomainException(
                $"Empty stock, product item {Name} is sold out");
        }
        if (quantityDesired <= 0)
        {
            throw new eShop.Catalog.API.Infrastructure.Exceptions.CatalogDomainException(
                "Item units desired should be greater than zero");
        }
        var removed = Math.Min(quantityDesired, AvailableStock);
        AvailableStock -= removed;
        return removed;
    }
}
