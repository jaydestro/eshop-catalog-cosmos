using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace eShop.Catalog.API;

#nullable enable

public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
    {
        var vApi = app.NewVersionedApi("Catalog");
        var api = vApi.MapGroup("api/catalog").HasApiVersion(1, 0).HasApiVersion(2, 0);
        var v1 = vApi.MapGroup("api/catalog").HasApiVersion(1, 0);
        var v2 = vApi.MapGroup("api/catalog").HasApiVersion(2, 0);

        v1.MapGet("/items", GetAllItemsV1).WithName("ListItems").WithTags("Items");
        v2.MapGet("/items", GetAllItems).WithName("ListItems-V2").WithTags("Items");
        api.MapGet("/items/by", GetItemsByIds).WithName("BatchGetItems").WithTags("Items");
        api.MapGet("/items/{id:int}", GetItemById).WithName("GetItem").WithTags("Items");
        v1.MapGet("/items/by/{name:minlength(1)}", GetItemsByName).WithName("GetItemsByName").WithTags("Items");
        api.MapGet("/items/{id:int}/pic", GetItemPictureById).WithName("GetItemPicture").WithTags("Items");

        v1.MapGet("/items/withsemanticrelevance/{text:minlength(1)}", GetItemsBySemanticRelevanceV1)
            .WithName("GetRelevantItems").WithTags("Search");
        v2.MapGet("/items/withsemanticrelevance", GetItemsBySemanticRelevance)
            .WithName("GetRelevantItems-V2").WithTags("Search");

        v1.MapGet("/items/type/{typeId}/brand/{brandId?}", GetItemsByBrandAndTypeId)
            .WithName("GetItemsByTypeAndBrand").WithTags("Types");
        v1.MapGet("/items/type/all/brand/{brandId:int?}", GetItemsByBrandId)
            .WithName("GetItemsByBrand").WithTags("Brands");

        api.MapGet("/catalogtypes", GetAllTypes).WithName("ListItemTypes").WithTags("Types");
        api.MapGet("/catalogbrands", GetAllBrands).WithName("ListItemBrands").WithTags("Brands");

        v1.MapPut("/items", UpdateItemV1).WithName("UpdateItem").WithTags("Items");
        v2.MapPut("/items/{id:int}", UpdateItem).WithName("UpdateItem-V2").WithTags("Items");
        api.MapPost("/items", CreateItem).WithName("CreateItem");
        api.MapDelete("/items/{id:int}", DeleteItemById).WithName("DeleteItem");

        return app;
    }

    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetAllItemsV1(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services)
        => await GetAllItems(paginationRequest, services, null, null, null);

    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetAllItems(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The name of the item to return")] string? name,
        [Description("The type of items to return")] int? type,
        [Description("The brand of items to return")] int? brand)
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object>();
        if (name is not null)
        {
            whereClauses.Add("STARTSWITH(c.name, @name, true)");
            parameters["@name"] = name;
        }
        if (type is not null)
        {
            whereClauses.Add("c.catalogTypeId = @type");
            parameters["@type"] = type.Value;
        }
        if (brand is not null)
        {
            whereClauses.Add("c.catalogBrandId = @brand");
            parameters["@brand"] = brand.Value;
        }
        var where = whereClauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", whereClauses);

        var countQuery = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c{where}");
        var pageQuery = new QueryDefinition($"SELECT * FROM c{where} ORDER BY c.name OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", pageSize * pageIndex)
            .WithParameter("@limit", pageSize);
        foreach (var kv in parameters)
        {
            countQuery = countQuery.WithParameter(kv.Key, kv.Value);
            pageQuery = pageQuery.WithParameter(kv.Key, kv.Value);
        }

        var totalItems = await services.Context.QueryItemsAsync(countQuery, pageQuery);
        return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems.Total, totalItems.Items));
    }

    public static async Task<Ok<List<CatalogItem>>> GetItemsByIds(
        [AsParameters] CatalogServices services,
        [Description("List of ids for catalog items to return")] int[] ids)
    {
        if (ids is null || ids.Length == 0)
        {
            return TypedResults.Ok(new List<CatalogItem>());
        }
        var tasks = ids.Distinct().Select(async id =>
        {
            var item = await services.Context.FindItemAsync(id);
            return item;
        });
        var found = (await Task.WhenAll(tasks)).Where(i => i is not null).Cast<CatalogItem>().ToList();
        return TypedResults.Ok(found);
    }

    public static async Task<Results<Ok<CatalogItem>, NotFound, BadRequest<ProblemDetails>>> GetItemById(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        [Description("The catalog item id")] int id)
    {
        if (id <= 0)
        {
            return TypedResults.BadRequest<ProblemDetails>(new() { Detail = "Id is not valid" });
        }
        var item = await services.Context.FindItemAsync(id);
        return item is null ? TypedResults.NotFound() : TypedResults.Ok(item);
    }

    public static Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByName(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The name of the item to return")] string name)
        => GetAllItems(paginationRequest, services, name, null, null);

    public static async Task<Results<PhysicalFileHttpResult, NotFound>> GetItemPictureById(
        eShop.Catalog.API.Infrastructure.CatalogContext context,
        IWebHostEnvironment environment,
        [Description("The catalog item id")] int id)
    {
        var item = await context.FindItemAsync(id);
        if (item is null || item.PictureFileName is null)
        {
            return TypedResults.NotFound();
        }
        var path = GetFullPath(environment.ContentRootPath, item.PictureFileName);
        var ext = Path.GetExtension(item.PictureFileName) ?? string.Empty;
        var mime = GetImageMimeTypeFromImageFileExtension(ext);
        var lastModified = File.GetLastWriteTimeUtc(path);
        return TypedResults.PhysicalFile(path, mime, lastModified: lastModified);
    }

    public static Task<Ok<PaginatedItems<CatalogItem>>> GetItemsBySemanticRelevanceV1(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The text string to use when search for related items in the catalog")] string text)
        => GetItemsBySemanticRelevance(paginationRequest, services, text);

    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsBySemanticRelevance(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The text string to use when search for related items in the catalog"), Required, MinLength(1)] string text)
    {
        // No embedding model in this environment - fall back to name search.
        return await GetItemsByName(paginationRequest, services, text);
    }

    public static Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByBrandAndTypeId(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The type of items to return")] int typeId,
        [Description("The brand of items to return")] int? brandId)
        => GetAllItems(paginationRequest, services, null, typeId, brandId);

    public static Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByBrandId(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The brand of items to return")] int? brandId)
        => GetAllItems(paginationRequest, services, null, null, brandId);

    public static async Task<Ok<List<CatalogType>>> GetAllTypes([AsParameters] CatalogServices services)
    {
        return TypedResults.Ok(await services.Context.ListTypesAsync());
    }

    public static async Task<Ok<List<CatalogBrand>>> GetAllBrands([AsParameters] CatalogServices services)
    {
        return TypedResults.Ok(await services.Context.ListBrandsAsync());
    }

    public static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> UpdateItemV1(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        CatalogItem productToUpdate)
    {
        if (productToUpdate?.Id == null)
        {
            return TypedResults.BadRequest<ProblemDetails>(new() { Detail = "Item id must be provided in the request body." });
        }
        return await UpdateItem(httpContext, productToUpdate.Id, services, productToUpdate);
    }

    public static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> UpdateItem(
        HttpContext httpContext,
        [Description("The id of the catalog item to delete")] int id,
        [AsParameters] CatalogServices services,
        CatalogItem productToUpdate)
    {
        var existing = await services.Context.FindItemAsync(id);
        if (existing is null)
        {
            return TypedResults.NotFound<ProblemDetails>(new() { Detail = $"Item with id {id} not found." });
        }

        var priceChanged = existing.Price != productToUpdate.Price;
        var oldPrice = existing.Price;

        existing.Name = productToUpdate.Name;
        existing.Description = productToUpdate.Description;
        existing.Price = productToUpdate.Price;
        existing.PictureFileName = productToUpdate.PictureFileName;
        existing.CatalogTypeId = productToUpdate.CatalogTypeId;
        existing.CatalogBrandId = productToUpdate.CatalogBrandId;
        existing.AvailableStock = productToUpdate.AvailableStock;
        existing.RestockThreshold = productToUpdate.RestockThreshold;
        existing.MaxStockThreshold = productToUpdate.MaxStockThreshold;

        if (priceChanged)
        {
            var priceChangedEvent = new ProductPriceChangedIntegrationEvent(existing.Id, productToUpdate.Price, oldPrice);
            await services.Context.UpsertItemAsync(existing);
            await services.EventService.SaveEventAndCatalogContextChangesAsync(priceChangedEvent);
            await services.EventService.PublishThroughEventBusAsync(priceChangedEvent);
        }
        else
        {
            await services.Context.UpsertItemAsync(existing);
        }
        return TypedResults.Created($"/api/catalog/items/{id}");
    }

    public static async Task<Created> CreateItem(
        [AsParameters] CatalogServices services,
        CatalogItem product)
    {
        var item = new CatalogItem(product.Name)
        {
            Id = product.Id,
            CatalogBrandId = product.CatalogBrandId,
            CatalogBrandName = product.CatalogBrandName,
            CatalogTypeId = product.CatalogTypeId,
            CatalogTypeName = product.CatalogTypeName,
            Description = product.Description,
            PictureFileName = product.PictureFileName,
            Price = product.Price,
            AvailableStock = product.AvailableStock,
            RestockThreshold = product.RestockThreshold,
            MaxStockThreshold = product.MaxStockThreshold,
        };
        await services.Context.UpsertItemAsync(item);
        return TypedResults.Created($"/api/catalog/items/{item.Id}");
    }

    public static async Task<Results<NoContent, NotFound>> DeleteItemById(
        [AsParameters] CatalogServices services,
        [Description("The id of the catalog item to delete")] int id)
    {
        var item = await services.Context.FindItemAsync(id);
        if (item is null) return TypedResults.NotFound();
        await services.Context.DeleteItemAsync(id);
        return TypedResults.NoContent();
    }

    private static string GetImageMimeTypeFromImageFileExtension(string extension) => extension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    public static string GetFullPath(string contentRootPath, string pictureFileName) =>
        Path.Combine(contentRootPath, "Pics", pictureFileName);
}
