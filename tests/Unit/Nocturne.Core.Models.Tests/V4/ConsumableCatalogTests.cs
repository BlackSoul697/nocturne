using FluentAssertions;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.Core.Models.Tests.V4;

public class ConsumableCatalogTests
{
    [Fact]
    public void GetAll_ReturnsAllEntries()
    {
        var all = ConsumableCatalog.GetAll();
        all.Should().HaveCount(7);
        all.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetById_Sensor_ReturnsSensorEntry()
    {
        var entry = ConsumableCatalog.GetById("sensor");
        entry.Should().NotBeNull();
        entry!.ConsumableType.Should().Be(ConsumableType.Sensor);
        entry.ApplicableDeviceCategory.Should().Be(DeviceCategory.CGM);
        entry.IsHardCutoff.Should().BeTrue();
    }

    [Fact]
    public void GetById_InsulinInUse_IsUniversal()
    {
        var entry = ConsumableCatalog.GetById("insulin-in-use");
        entry.Should().NotBeNull();
        entry!.ApplicableDeviceCategory.Should().BeNull();
        entry.DefaultLifespanHours.Should().Be(672);
    }

    [Fact]
    public void GetForDevice_DexcomG7_ReturnsSensorOnly()
    {
        var device = DeviceCatalog.GetById("dexcom-g7")!;
        var consumables = ConsumableCatalog.GetForDevice(device);
        consumables.Select(c => c.Id).Should().Contain("sensor");
        consumables.Select(c => c.Id).Should().NotContain("transmitter");
        consumables.Select(c => c.Id).Should().Contain("insulin-in-use");
    }

    [Fact]
    public void GetForDevice_DexcomG6_ReturnsSensorAndTransmitter()
    {
        var device = DeviceCatalog.GetById("dexcom-g6")!;
        var consumables = ConsumableCatalog.GetForDevice(device);
        consumables.Select(c => c.Id).Should().Contain("sensor");
        consumables.Select(c => c.Id).Should().Contain("transmitter");
    }

    [Fact]
    public void GetById_NonExistent_ReturnsNull()
    {
        ConsumableCatalog.GetById("does-not-exist").Should().BeNull();
    }
}
