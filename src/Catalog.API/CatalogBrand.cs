using System.Text.Json.Serialization;

namespace eShop.Catalog.API.Model;

public class CatalogBrand
{
    public CatalogBrand() { Brand = string.Empty; }
    public CatalogBrand(string brand) { Brand = brand; }

    public int Id { get; set; }
    public string Brand { get; set; }
}
