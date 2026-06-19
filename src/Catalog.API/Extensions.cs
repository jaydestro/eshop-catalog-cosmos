using Microsoft.Azure.Cosmos;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<CatalogOptions>()
            .BindConfiguration(nameof(CatalogOptions));

        // Always register the typed services so the API surface works in build-time OpenAPI mode.
        builder.Services.AddSingleton<CatalogContext>(sp =>
        {
            var client = sp.GetService<CosmosClient>();
            return client is null ? null! : new CatalogContext(client);
        });
        builder.Services.AddTransient<ICatalogIntegrationEventService, CatalogIntegrationEventService>();
        builder.Services.AddTransient<IIntegrationEventLogService>(sp =>
        {
            var ctx = sp.GetService<CatalogContext>();
            return ctx is null ? null! : new CosmosIntegrationEventLogService(ctx.Events, Assembly.GetEntryAssembly()!);
        });

        // Avoid wiring the real Cosmos client and bootstrap when generating OpenAPI at build time.
        if (builder.Environment.IsBuild())
        {
            return;
        }

        builder.AddCosmosClientSingleton();
        builder.Services.AddHostedService<CatalogBootstrapHostedService>();

        builder.AddRabbitMqEventBus("eventbus")
               .AddSubscription<OrderStatusChangedToAwaitingValidationIntegrationEvent, OrderStatusChangedToAwaitingValidationIntegrationEventHandler>()
               .AddSubscription<OrderStatusChangedToPaidIntegrationEvent, OrderStatusChangedToPaidIntegrationEventHandler>();
    }
}

/// <summary>
/// Provisions the catalog Cosmos DB database/containers and seeds initial data at startup.
/// Retries while the emulator is still warming up.
/// </summary>
internal sealed class CatalogBootstrapHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CatalogBootstrapHostedService> _logger;

    public CatalogBootstrapHostedService(IServiceProvider services, ILogger<CatalogBootstrapHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                using var scope = _services.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<CosmosClient>();
                await CatalogContext.EnsureCreatedAsync(client);
                var ctx = scope.ServiceProvider.GetRequiredService<CatalogContext>();
                var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var seeder = new CatalogContextSeed(ctx, env,
                    scope.ServiceProvider.GetRequiredService<ILogger<CatalogContextSeed>>());
                await seeder.SeedAsync();
                _logger.LogInformation("Catalog bootstrap complete");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog bootstrap attempt {Attempt} failed; retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
