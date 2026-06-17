using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Constants;

namespace Nocturne.Connectors.Glooko.Configurations;

/// <summary>
///     Configuration specific to Glooko connector
/// </summary>
[ConnectorRegistration(
    "Glooko",
    ServiceNames.GlookoConnector,
    "GLOOKO",
    "ConnectSource.Glooko",
    "glooko-connector",
    "glooko",
    ConnectorCategory.Sync,
    "Import data from Glooko platform",
    "Glooko",
    SupportsHistoricalSync = true,
    SupportsManualSync = true,
    DefaultActiveThresholdMinutes = 180,
    DefaultStaleThresholdMinutes = 360,
    SupportedDataTypes = [
        SyncDataType.Glucose,
        SyncDataType.Boluses,
        SyncDataType.BasalInjections,
        SyncDataType.CarbIntake,
        SyncDataType.StateSpans,
        SyncDataType.DeviceEvents,
        SyncDataType.Profiles
    ]
)]
public class GlookoConnectorConfiguration : BaseConnectorConfiguration
{
    public GlookoConnectorConfiguration()
    {
        ConnectSource = ConnectSource.Glooko;
    }

    /// <summary>
    ///     Glooko account email
    /// </summary>
    [ConnectorProperty(ConnectorPropertyKey.Email, Required = true)]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    ///     Glooko account password
    /// </summary>
    [ConnectorProperty(ConnectorPropertyKey.Password, Required = true, Secret = true)]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    ///     Glooko server region.
    /// </summary>
    [ConnectorProperty(ConnectorPropertyKey.Server,
        DefaultValue = GlookoConstants.RegionUS,
        AllowedValues = [GlookoConstants.RegionCA, GlookoConstants.RegionEU, GlookoConstants.RegionUS])]
    public string Server { get; init; } = GlookoConstants.RegionUS;

    /// <summary>
    ///     Use v3 API for additional data types (alarms, automatic boluses, consumables).
    ///     This provides a single API call instead of multiple v2 calls.
    /// </summary>
    [ConnectorProperty(ConnectorPropertyKey.UseV3Api, DefaultValue = "true")]
    public bool UseV3Api { get; set; } = true;

    /// <summary>
    ///     Include CGM readings from v3 as backup to primary CGM source (e.g., xDrip).
    ///     Only use this if you want Glooko to fill gaps in your primary CGM data.
    /// </summary>
    [ConnectorProperty(ConnectorPropertyKey.V3IncludeCgmBackfill, DefaultValue = "false")]
    public bool V3IncludeCgmBackfill { get; set; } = false;

    /// <summary>
    ///     Source glucose from the Glooko mobile app's granular SSV2 sync endpoint
    ///     (<c>/api/v2/cgm/egvs</c>) instead of the web graph/batch flow. The egvs feed is the
    ///     raw per-reading CGM stream (trend, system vs display time) the app itself consumes,
    ///     paginated by cursor. Experimental and currently glucose-only — when enabled, glucose
    ///     comes from egvs while all other data types continue via the v2/v3 path selected by
    ///     <see cref="UseV3Api"/>.
    /// </summary>
    [ConnectorProperty(ConnectorPropertyKey.UseSsv2Sync, DefaultValue = "false")]
    public bool UseSsv2Sync { get; set; } = false;
}
