using Nocturne.Connectors.TandemSource.Configurations;
using Nocturne.Connectors.TandemSource.Services;

namespace Nocturne.API.Services.BackgroundServices;

public class TandemSourceConnectorBackgroundService
    : ConnectorBackgroundService<TandemSourceConnectorConfiguration>
{
    public TandemSourceConnectorBackgroundService(
        IServiceProvider serviceProvider,
        TandemSourceConnectorConfiguration config,
        ILogger<TandemSourceConnectorBackgroundService> logger
    ) : base(serviceProvider, config, logger) { }

    protected override string ConnectorName => "TandemSource";

    protected override async Task<bool> PerformSyncAsync(IServiceProvider scopeProvider, CancellationToken cancellationToken)
    {
        var connectorService = scopeProvider.GetRequiredService<TandemSourceConnectorService>();
        return await connectorService.SyncDataAsync(Config, cancellationToken);
    }
}
