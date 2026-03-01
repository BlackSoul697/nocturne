using System.Text.Json.Serialization;

namespace Nocturne.Connectors.TandemSource.Models;

public class PumpEventMetadata
{
    [JsonPropertyName("tconnectDeviceId")]
    public string TConnectDeviceId { get; set; } = default!;

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("modelNumber")]
    public string? ModelNumber { get; set; }

    [JsonPropertyName("minDateWithEvents")]
    public string? MinDateWithEvents { get; set; }

    [JsonPropertyName("maxDateWithEvents")]
    public string? MaxDateWithEvents { get; set; }

    [JsonPropertyName("lastUpload")]
    public PumpLastUpload? LastUpload { get; set; }

    [JsonPropertyName("softwareVersion")]
    public string? SoftwareVersion { get; set; }
}

public class PumpLastUpload
{
    [JsonPropertyName("settings")]
    public PumpSettings? Settings { get; set; }
}

public class PumpSettings
{
    [JsonPropertyName("profiles")]
    public PumpProfiles? Profiles { get; set; }

    [JsonPropertyName("cgmSettings")]
    public PumpCgmSettings? CgmSettings { get; set; }
}

public class PumpProfiles
{
    [JsonPropertyName("activeIdp")]
    public int ActiveIdp { get; set; }

    [JsonPropertyName("profile")]
    public List<PumpProfile> Profile { get; set; } = [];
}

public class PumpProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("idp")]
    public int Idp { get; set; }

    [JsonPropertyName("tDependentSegs")]
    public List<PumpProfileSegment> TDependentSegs { get; set; } = [];

    [JsonPropertyName("insulinDuration")]
    public int InsulinDuration { get; set; }

    [JsonPropertyName("carbEntry")]
    public int CarbEntry { get; set; }

    [JsonPropertyName("maxBolus")]
    public int MaxBolus { get; set; }
}

public class PumpProfileSegment
{
    [JsonPropertyName("startTime")]
    public int StartTime { get; set; }

    [JsonPropertyName("basalRate")]
    public int BasalRate { get; set; }

    [JsonPropertyName("isf")]
    public int Isf { get; set; }

    [JsonPropertyName("carbRatio")]
    public int CarbRatio { get; set; }

    [JsonPropertyName("targetBg")]
    public int TargetBg { get; set; }
}

public class PumpCgmSettings
{
    [JsonPropertyName("highGlucoseAlert")]
    public PumpGlucoseAlert? HighGlucoseAlert { get; set; }

    [JsonPropertyName("lowGlucoseAlert")]
    public PumpGlucoseAlert? LowGlucoseAlert { get; set; }
}

public class PumpGlucoseAlert
{
    [JsonPropertyName("mgPerDl")]
    public int MgPerDl { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}
