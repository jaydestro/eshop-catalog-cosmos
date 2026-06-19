namespace eShop.Catalog.API.IntegrationEvents.EventHandling;

public class OrderStatusChangedToPaidIntegrationEventHandler(
    CatalogContext catalogContext,
    ILogger<OrderStatusChangedToPaidIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderStatusChangedToPaidIntegrationEvent>
{
    public async Task Handle(OrderStatusChangedToPaidIntegrationEvent @event)
    {
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        foreach (var orderStockItem in @event.OrderStockItems)
        {
            var catalogItem = await catalogContext.FindItemAsync(orderStockItem.ProductId);
            if (catalogItem is null) continue;
            catalogItem.RemoveStock(orderStockItem.Units);
            await catalogContext.UpsertItemAsync(catalogItem);
        }
    }
}
