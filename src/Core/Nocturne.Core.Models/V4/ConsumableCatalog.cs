namespace Nocturne.Core.Models.V4;

/// <summary>
/// Static catalog of known consumable items that can be tracked.
/// </summary>
public static class ConsumableCatalog
{
    private static readonly IReadOnlyList<ConsumableCatalogEntry> _entries =
    [
        new() { Id = "sensor",         Name = "Sensor",            ConsumableType = ConsumableType.Sensor,        DefaultLifespanHours = null, IsHardCutoff = true,  ApplicableDeviceCategory = DeviceCategory.CGM,         DefaultTrackerCategory = TrackerCategory.Sensor,     DefaultIcon = "activity" },
        new() { Id = "transmitter",    Name = "Transmitter",       ConsumableType = ConsumableType.Transmitter,   DefaultLifespanHours = null, IsHardCutoff = true,  ApplicableDeviceCategory = DeviceCategory.CGM,         DefaultTrackerCategory = TrackerCategory.Battery,    DefaultIcon = "radio" },
        new() { Id = "pod",            Name = "Pod",               ConsumableType = ConsumableType.Pod,           DefaultLifespanHours = 80,   IsHardCutoff = true,  ApplicableDeviceCategory = DeviceCategory.InsulinPump, DefaultTrackerCategory = TrackerCategory.Cannula,    DefaultIcon = "package" },
        new() { Id = "infusion-set",   Name = "Infusion Set",      ConsumableType = ConsumableType.InfusionSet,   DefaultLifespanHours = 72,   IsHardCutoff = false, ApplicableDeviceCategory = DeviceCategory.InsulinPump, DefaultTrackerCategory = TrackerCategory.Cannula,    DefaultIcon = "syringe" },
        new() { Id = "reservoir",      Name = "Reservoir",         ConsumableType = ConsumableType.Reservoir,     DefaultLifespanHours = null, IsHardCutoff = false, ApplicableDeviceCategory = DeviceCategory.InsulinPump, DefaultTrackerCategory = TrackerCategory.Reservoir,  DefaultIcon = "flask-round" },
        new() { Id = "insulin-tubing", Name = "Insulin Tubing",    ConsumableType = ConsumableType.InsulinTubing, DefaultLifespanHours = 72,   IsHardCutoff = false, ApplicableDeviceCategory = DeviceCategory.InsulinPump, DefaultTrackerCategory = TrackerCategory.Consumable, DefaultIcon = "cable" },
        new() { Id = "insulin-in-use", Name = "Insulin (In Use)",  ConsumableType = ConsumableType.InsulinInUse,  DefaultLifespanHours = 672,  IsHardCutoff = false, ApplicableDeviceCategory = null,                       DefaultTrackerCategory = TrackerCategory.Consumable, DefaultIcon = "droplets" },
    ];

    /// <summary>Returns all known consumable entries.</summary>
    public static IReadOnlyList<ConsumableCatalogEntry> GetAll() => _entries;

    /// <summary>Looks up a consumable by its unique identifier.</summary>
    public static ConsumableCatalogEntry? GetById(string id) =>
        _entries.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Returns consumables applicable to a specific device, plus universal consumables.
    /// Filters by device category. For CGMs, filters transmitter based on
    /// <see cref="CgmProperties.HasSeparateTransmitter"/>.
    /// </summary>
    /// <remarks>
    /// Pump form factor filtering (patch vs tubed) will be added once PumpProperties
    /// is available on DeviceCatalogEntry.
    /// </remarks>
    public static IReadOnlyList<ConsumableCatalogEntry> GetForDevice(DeviceCatalogEntry device) =>
        _entries.Where(e => AppliesToDevice(e, device)).ToList();

    private static bool AppliesToDevice(ConsumableCatalogEntry entry, DeviceCatalogEntry device)
    {
        // Universal consumables apply to all devices
        if (entry.ApplicableDeviceCategory is null)
            return true;

        // Must match device category
        if (entry.ApplicableDeviceCategory != device.Category)
            return false;

        // CGM-specific: transmitter only when device has a separate transmitter
        if (entry.ConsumableType == ConsumableType.Transmitter)
            return device.Cgm?.HasSeparateTransmitter == true;

        // TODO: Filter pump consumables by PumpProperties.FormFactor once available
        // Pod → Patch only, InfusionSet/Reservoir/InsulinTubing → Tubed only

        return true;
    }
}
