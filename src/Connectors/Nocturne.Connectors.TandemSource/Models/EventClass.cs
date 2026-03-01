namespace Nocturne.Connectors.TandemSource.Models;

public enum EventClass
{
    Basal,
    BasalSuspension,
    BasalResume,
    Alarm,
    Bolus,
    Cartridge,
    CgmAlert,
    CgmStartJoinStop,
    CgmReading,
    UserMode,
    DeviceStatus
}
