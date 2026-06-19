using Microsoft.Azure.Cosmos;
using System.Net;
using eShop.Catalog.API.Model;

namespace eShop.Catalog.API.Infrastructure;

#nullable enable

/// <summary>
/// Cosmos DB-backed replacement for the original EF Core CatalogContext.
/// Containers:
///   - items: /id   (each item is its own logical partition; supports point reads on item id
///                   and parallel cross-partition queries for browse)
///   - brands: /id  (small reference set)
///   - types: /id   (small reference set)
///   - events: /id  (integration event outbox)
/// </summary>
public sealed class CatalogContext
{
    public const string DatabaseId = "catalog";
    public const string ItemsContainer = "items";
    public const string BrandsContainer = "brands";
    public const string TypesContainer = "types";
    public const string EventsContainer = "events";

    public CatalogContext(CosmosClient client)
    {
        Client = client;
        Database = client.GetDatabase(DatabaseId);
        Items = Database.GetContainer(ItemsContainer);
        Brands = Database.GetContainer(BrandsContainer);
        Types = Database.GetContainer(TypesContainer);
        Events = Database.GetContainer(EventsContainer);
    }

    public CosmosClient Client { get; }
    public Database Database { get; }
    public Container Items { get; }
    public Container Brands { get; }
    public Container Types { get; }
    public Container Events { get; }

    public static async Task EnsureCreatedAsync(CosmosClient client)
    {
        await client.CreateDatabaseIfNotExistsAsync(DatabaseId);
        var db = client.GetDatabase(DatabaseId);
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties(ItemsContainer, "/id"));
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties(BrandsContainer, "/id"));
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties(TypesContainer, "/id"));
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties(EventsContainer, "/id"));
    }

    public async Task<CatalogItem?> FindItemAsync(int id)
    {
        try
        {
            var resp = await Items.ReadItemAsync<CatalogItemDocument>(
                id.ToString(), new PartitionKey(id.ToString()));
            return resp.Resource.ToCatalogItem();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task UpsertItemAsync(CatalogItem item)
    {
        var doc = CatalogItemDocument.From(item);
        return Items.UpsertItemAsync(doc, new PartitionKey(doc.Id));
    }

    public async Task DeleteItemAsync(int id)
    {
        try
        {
            await Items.DeleteItemAsync<CatalogItemDocument>(
                id.ToString(), new PartitionKey(id.ToString()));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
    }

    public async Task<(long Total, List<CatalogItem> Items)> QueryItemsAsync(QueryDefinition countQuery, QueryDefinition pageQuery)
    {
        var totals = new List<long>();
        using (var iter = Items.GetItemQueryIterator<long>(countQuery))
        {
            while (iter.HasMoreResults) totals.AddRange(await iter.ReadNextAsync());
        }

        var docs = new List<CatalogItemDocument>();
        using (var iter = Items.GetItemQueryIterator<CatalogItemDocument>(pageQuery))
        {
            while (iter.HasMoreResults) docs.AddRange(await iter.ReadNextAsync());
        }
        return (totals.FirstOrDefault(), docs.Select(d => d.ToCatalogItem()).ToList());
    }

    public async Task<List<CatalogBrand>> ListBrandsAsync()
    {
        var docs = new List<CatalogBrandDocument>();
        using var iter = Brands.GetItemQueryIterator<CatalogBrandDocument>(new QueryDefinition("SELECT * FROM c ORDER BY c.brand"));
        while (iter.HasMoreResults) docs.AddRange(await iter.ReadNextAsync());
        return docs.Select(d => d.ToBrand()).ToList();
    }

    public async Task<List<CatalogType>> ListTypesAsync()
    {
        var docs = new List<CatalogTypeDocument>();
        using var iter = Types.GetItemQueryIterator<CatalogTypeDocument>(new QueryDefinition("SELECT * FROM c ORDER BY c.type"));
        while (iter.HasMoreResults) docs.AddRange(await iter.ReadNextAsync());
        return docs.Select(d => d.ToType()).ToList();
    }
}
