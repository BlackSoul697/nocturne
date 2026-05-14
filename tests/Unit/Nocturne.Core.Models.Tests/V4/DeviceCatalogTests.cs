using FluentAssertions;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.Core.Models.Tests.V4;

[Trait("Category", "Unit")]
public class DeviceCatalogTests
{
    [Fact]
    public void GetAll_ShouldReturnAllEntries()
    {
        var entries = DeviceCatalog.GetAll();
        entries.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAll_ShouldContainBothCgmsAndPumps()
    {
        var entries = DeviceCatalog.GetAll();
        entries.Should().Contain(e => e.Category == DeviceCategory.CGM);
        entries.Should().Contain(e => e.Category == DeviceCategory.InsulinPump);
    }

    [Fact]
    public void GetById_ShouldReturnMatchingEntry()
    {
        var entry = DeviceCatalog.GetById("dexcom-g7");
        entry.Should().NotBeNull();
        entry!.Name.Should().Be("Dexcom G7");
        entry.Category.Should().Be(DeviceCategory.CGM);
    }

    [Fact]
    public void GetById_ShouldReturnNullForUnknownId()
    {
        var entry = DeviceCatalog.GetById("nonexistent");
        entry.Should().BeNull();
    }

    [Fact]
    public void GetByCategory_ShouldFilterCorrectly()
    {
        var cgms = DeviceCatalog.GetByCategory(DeviceCategory.CGM);
        cgms.Should().OnlyContain(e => e.Category == DeviceCategory.CGM);
    }

    [Fact]
    public void GetByCategory_ForUnsupportedCategory_ShouldReturnEmpty()
    {
        var meters = DeviceCatalog.GetByCategory(DeviceCategory.GlucoseMeter);
        meters.Should().BeEmpty();
    }

    [Fact]
    public void CgmEntries_ShouldHaveCgmProperties()
    {
        var cgms = DeviceCatalog.GetByCategory(DeviceCategory.CGM);
        cgms.Where(e => e.Id != "custom-cgm")
            .Should().OnlyContain(e => e.Cgm != null);
    }

    [Fact]
    public void PumpEntries_ShouldNotHaveCgmProperties()
    {
        var pumps = DeviceCatalog.GetByCategory(DeviceCategory.InsulinPump);
        pumps.Should().OnlyContain(e => e.Cgm == null);
    }

    [Fact]
    public void CgmWithSeparateTransmitter_ShouldHaveTransmitterDuration()
    {
        var g6 = DeviceCatalog.GetById("dexcom-g6");
        g6.Should().NotBeNull();
        g6!.Cgm!.HasSeparateTransmitter.Should().BeTrue();
        g6.Cgm.TransmitterDurationDays.Should().NotBeNull();
    }

    [Fact]
    public void CgmWithoutSeparateTransmitter_ShouldNotHaveTransmitterDuration()
    {
        var g7 = DeviceCatalog.GetById("dexcom-g7");
        g7.Should().NotBeNull();
        g7!.Cgm!.HasSeparateTransmitter.Should().BeFalse();
        g7.Cgm.TransmitterDurationDays.Should().BeNull();
    }

    [Fact]
    public void AllEntries_ShouldHaveUniqueIds()
    {
        var entries = DeviceCatalog.GetAll();
        entries.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Omnipod5_HasPatchFormFactor()
    {
        var entry = DeviceCatalog.GetById("omnipod-5");
        entry.Should().NotBeNull();
        entry!.Pump.Should().NotBeNull();
        entry.Pump!.FormFactor.Should().Be(PumpFormFactor.Patch);
        entry.Pump.ReservoirCapacityUnits.Should().Be(200);
    }

    [Fact]
    public void TslimX2_HasTubedFormFactor()
    {
        var entry = DeviceCatalog.GetById("tandem-tslim-x2");
        entry.Should().NotBeNull();
        entry!.Pump.Should().NotBeNull();
        entry.Pump!.FormFactor.Should().Be(PumpFormFactor.Tubed);
        entry.Pump.ReservoirCapacityUnits.Should().Be(300);
    }

    [Fact]
    public void CgmEntries_HaveNoPumpProperties()
    {
        var entry = DeviceCatalog.GetById("dexcom-g7");
        entry.Should().NotBeNull();
        entry!.Pump.Should().BeNull();
    }

    [Fact]
    public void CustomPump_HasNoPumpProperties()
    {
        var entry = DeviceCatalog.GetById("custom-pump");
        entry.Should().NotBeNull();
        entry!.Pump.Should().BeNull();
    }
}
