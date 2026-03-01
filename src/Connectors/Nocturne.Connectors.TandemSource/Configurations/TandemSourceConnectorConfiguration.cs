using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.TandemSource.Configurations;

[ConnectorRegistration(
    "TandemSource",
    ServiceNames.TandemSourceConnector,
    "TANDEMSOURCE",
    "ConnectSource.TandemSource",
    "tandemsource-connector",
    "tandemsource",
    ConnectorCategory.Pump,
    "Connect to Tandem Source for t:slim X2 and Mobi pump data",
    "Tandem Source",
    SupportsHistoricalSync = true,
    SupportsManualSync = true,
    SupportedDataTypes = [
        SyncDataType.Glucose,
        SyncDataType.Boluses,
        SyncDataType.DeviceEvents,
        SyncDataType.StateSpans,
        SyncDataType.Profiles
    ]
)]
public class TandemSourceConnectorConfiguration : BaseConnectorConfiguration
{
    public TandemSourceConnectorConfiguration()
    {
        ConnectSource = ConnectSource.TandemSource;
    }

    [ConnectorProperty(ConnectorPropertyKey.Email, Required = true)]
    public string Email { get; set; } = string.Empty;

    [ConnectorProperty(ConnectorPropertyKey.Password, Required = true, Secret = true)]
    public string Password { get; set; } = string.Empty;

    [ConnectorProperty(ConnectorPropertyKey.Server, DefaultValue = "US", AllowedValues = ["US", "EU"])]
    public string Server { get; set; } = "US";

    [ConnectorProperty(ConnectorPropertyKey.Timezone, Required = true, DefaultValue = "America/New_York")]
    public string Timezone { get; set; } = "America/New_York";
}
