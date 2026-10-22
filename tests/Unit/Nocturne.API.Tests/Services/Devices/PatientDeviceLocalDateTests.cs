using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services.Devices;
using Nocturne.API.Tests.TestDoubles;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Tests.Shared.Mocks;
using Xunit;

namespace Nocturne.API.Tests.Services.Devices;

/// <summary>
/// A device's dates are local calendar dates, so the two ingest paths that match a record to a
/// device, <see cref="DeviceService.ResolvePatientDeviceAsync"/> and
/// <see cref="PatientDeviceStamper"/>, must pick the same device for a record in the hours where the
/// wearer's date is already the next one.
/// </summary>
[Trait("Category", "Unit")]
public class PatientDeviceLocalDateTests
{
    private const string Zone = "Australia/Adelaide";

    private readonly Guid _deviceId = Guid.CreateVersion7();
    private readonly PatientDevice _outgoing;
    private readonly PatientDevice _incoming;
    private readonly Mock<IPatientDeviceRepository> _patientDevices = new();

    public PatientDeviceLocalDateTests()
    {
        _outgoing = Pump(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10));
        _incoming = Pump(new DateOnly(2026, 6, 11), null);
        _patientDevices
            .Setup(r => r.GetByDeviceIdAsync(_deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_outgoing, _incoming]);
        _patientDevices
            .Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_outgoing, _incoming]);
    }

    // 2026-06-10 15:00Z is 00:30 on 11 June in Adelaide (+9:30): the new device's first day.
    private static readonly DateTime FirstHoursOfTheNewDay = new(2026, 6, 10, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DeviceService_MatchesARecordInTheZoneOffsetHoursOnTheLocalDate()
    {
        var service = new DeviceService(
            Mock.Of<IDeviceRepository>(),
            _patientDevices.Object,
            MockTenantAccessor.Create().Object,
            TimezoneTimelineDoubles.In(Zone));

        var matched = await service.ResolvePatientDeviceAsync(
            _deviceId, new DateTimeOffset(FirstHoursOfTheNewDay).ToUnixTimeMilliseconds());

        matched.Should().Be(_incoming.Id);
    }

    [Fact]
    public async Task Stamper_MatchesARecordInTheZoneOffsetHoursOnTheLocalDate()
    {
        var stamper = new PatientDeviceStamper(
            _patientDevices.Object, TimezoneTimelineDoubles.In(Zone), NullLogger<PatientDeviceStamper>.Instance);
        var record = new SensorGlucose { Mgdl = 120, Timestamp = FirstHoursOfTheNewDay };

        await stamper.StampAsync([record], [DeviceCategory.InsulinPump], batchSource: null);

        record.PatientDeviceId.Should().Be(_incoming.Id);
    }

    private PatientDevice Pump(DateOnly start, DateOnly? end) => new()
    {
        Id = Guid.CreateVersion7(),
        DeviceId = _deviceId,
        DeviceCategory = DeviceCategory.InsulinPump,
        StartDate = start,
        EndDate = end,
    };
}
