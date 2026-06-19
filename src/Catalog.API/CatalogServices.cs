namespace eShop.Catalog.API.Model;

public class CatalogServices
{
    public CatalogServices(
        eShop.Catalog.API.Infrastructure.CatalogContext context,
        IOptions<CatalogOptions> options,
        ILogger<CatalogServices> logger,
        eShop.Catalog.API.IntegrationEvents.ICatalogIntegrationEventService eventService)
    {
        Context = context;
        Options = options;
        Logger = logger;
        EventService = eventService;
    }

    public eShop.Catalog.API.Infrastructure.CatalogContext Context { get; }
    public IOptions<CatalogOptions> Options { get; }
    public ILogger<CatalogServices> Logger { get; }
    public eShop.Catalog.API.IntegrationEvents.ICatalogIntegrationEventService EventService { get; }
}
