using System.Text.Json.Serialization;

namespace eShop.Catalog.API.Model;

public class CatalogType
{
    public CatalogType() { Type = string.Empty; }
    public CatalogType(string type) { Type = type; }

    public int Id { get; set; }
    public string Type { get; set; }
}
