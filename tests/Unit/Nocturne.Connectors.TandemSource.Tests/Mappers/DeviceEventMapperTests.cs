using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Mappers;
using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.Mappers;

public class DeviceEventMapperTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Utc;

    private static ParsedEvent CreateParsedEvent(string eventName, int eventId, uint timestamp, uint seqNum)
    {
        return new ParsedEvent
        {
            EventId = eventId,
            EventName = eventName,
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = new byte[16],
            FieldDefinitions = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ParsedEvent CreateAlarmEvent(uint timestamp, uint seqNum, uint alarmId)
    {
        var payload = new byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0), alarmId);
        var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlarmID"] = new EventFieldDefinition { Type = "uint32", Offset = 0 }
        };
        return new ParsedEvent
        {
            EventId = 5,
            EventName = "LID_ALARM_ACTIVATED",
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = payload,
            FieldDefinitions = fields
        };
    }

    [Fact]
    public void Map_CartridgeFilled_ReturnsReservoirChange()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.Cartridge] = [CreateParsedEvent("LID_CARTRIDGE_FILLED", 33, 1000, 1)]
        };

        var result = TandemSourceDeviceEventMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be(DeviceEventType.ReservoirChange);
        result[0].Notes.Should().Be("Cartridge filled");
    }

    [Fact]
    public void Map_CannulaFilled_ReturnsSiteChange()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.Cartridge] = [CreateParsedEvent("LID_CANNULA_FILLED", 61, 1000, 1)]
        };

        var result = TandemSourceDeviceEventMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be(DeviceEventType.SiteChange);
        result[0].Notes.Should().Be("Cannula filled");
    }

    [Fact]
    public void Map_TubingFilled_ReturnsPriming()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.Cartridge] = [CreateParsedEvent("LID_TUBING_FILLED", 63, 1000, 1)]
        };

        var result = TandemSourceDeviceEventMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be(DeviceEventType.Priming);
        result[0].Notes.Should().Be("Tubing filled");
    }

    [Fact]
    public void Map_CgmStartSession_ReturnsSensorStart()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.CgmStartJoinStop] = [CreateParsedEvent("LID_CGM_START_SESSION_GX", 212, 1000, 1)]
        };

        var result = TandemSourceDeviceEventMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be(DeviceEventType.SensorStart);
        result[0].Notes.Should().Be("CGM Session Started");
    }

    [Fact]
    public void Map_CgmStopSession_ReturnsSensorStop()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.CgmStartJoinStop] = [CreateParsedEvent("LID_CGM_STOP_SESSION_GX", 214, 1000, 1)]
        };

        var result = TandemSourceDeviceEventMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be(DeviceEventType.SensorStop);
        result[0].Notes.Should().Be("CGM Session Stopped");
    }

    [Fact]
    public void Map_AlarmActivated_ReturnsPumpSuspend()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.Alarm] = [CreateAlarmEvent(1000, 1, 42)]
        };

        var result = TandemSourceDeviceEventMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be(DeviceEventType.PumpSuspend);
        result[0].Notes.Should().Contain("Alarm");
        result[0].Notes.Should().Contain("42");
    }

    [Fact]
    public void Map_EmptyClassified_ReturnsEmpty()
    {
        var result = TandemSourceDeviceEventMapper.Map(new Dictionary<EventClass, List<ParsedEvent>>(), Tz);
        result.Should().BeEmpty();
    }
}
