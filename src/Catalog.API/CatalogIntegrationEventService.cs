namespace eShop.Catalog.API.IntegrationEvents;

/// <summary>
/// Cosmos DB-friendly catalog outbox service. Without multi-document transactions we just
/// write the event to the outbox container first, then publish through the bus.
/// </summary>
public sealed class CatalogIntegrationEventService : ICatalogIntegrationEventService, IDisposable
{
    private readonly ILogger<CatalogIntegrationEventService> _logger;
    private readonly IEventBus _eventBus;
    private readonly IIntegrationEventLogService _eventLogService;
    private volatile bool _disposed;

    public CatalogIntegrationEventService(
        ILogger<CatalogIntegrationEventService> logger,
        IEventBus eventBus,
        IIntegrationEventLogService eventLogService)
    {
        _logger = logger;
        _eventBus = eventBus;
        _eventLogService = eventLogService;
    }

    public async Task PublishThroughEventBusAsync(IntegrationEvent evt)
    {
        try
        {
            _logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", evt.Id, evt);
            await _eventLogService.MarkEventAsInProgressAsync(evt.Id);
            await _eventBus.PublishAsync(evt);
            await _eventLogService.MarkEventAsPublishedAsync(evt.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Publishing integration event: {IntegrationEventId}", evt.Id);
            await _eventLogService.MarkEventAsFailedAsync(evt.Id);
        }
    }

    public Task SaveEventAndCatalogContextChangesAsync(IntegrationEvent evt)
    {
        _logger.LogInformation("Saving integrationEvent: {IntegrationEventId}", evt.Id);
        return _eventLogService.SaveEventAsync(evt, Guid.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_eventLogService as IDisposable)?.Dispose();
    }
}
