using System.Buffers.Binary;
using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Mappers;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.Mappers;

public class SensorGlucoseMapperTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Utc;

    private static ParsedEvent CreateCgmEvent(uint timestamp, uint seqNum,
        ushort glucoseValue, uint egvTimestamp)
    {
        // LID_CGM_DATA_GXB layout: Rate@0, CGM Data Type@1, glucoseValueStatus@2, currentGlucoseDisplayValue@4, EGV TimeStamp@8
        var payload = new byte[16];
        var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(4), glucoseValue);
        fields["currentGlucoseDisplayValue"] = new EventFieldDefinition { Type = "uint16", Offset = 4 };

        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), egvTimestamp);
        fields["EGV TimeStamp"] = new EventFieldDefinition { Type = "uint32", Offset = 8 };

        return new ParsedEvent
        {
            EventId = 171,
            EventName = "LID_CGM_DATA_GXB",
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = payload,
            FieldDefinitions = fields
        };
    }

    [Fact]
    public void Map_ValidReading_CreatesSensorGlucose()
    {
        var events = new List<ParsedEvent>
        {
            CreateCgmEvent(1000, 1, glucoseValue: 120, egvTimestamp: 999)
        };

        var result = TandemSourceSensorGlucoseMapper.Map(events, Tz);

        result.Should().HaveCount(1);
        result[0].Mgdl.Should().Be(120);
    }

    [Fact]
    public void Map_ZeroGlucose_IsSkipped()
    {
        var events = new List<ParsedEvent>
        {
            CreateCgmEvent(1000, 1, glucoseValue: 0, egvTimestamp: 999)
        };

        var result = TandemSourceSensorGlucoseMapper.Map(events, Tz);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Map_MultipleReadings_SortsByTimestamp()
    {
        var events = new List<ParsedEvent>
        {
            CreateCgmEvent(2000, 2, glucoseValue: 130, egvTimestamp: 1999),
            CreateCgmEvent(1000, 1, glucoseValue: 120, egvTimestamp: 999),
        };

        var result = TandemSourceSensorGlucoseMapper.Map(events, Tz);

        result.Should().HaveCount(2);
        result[0].Mgdl.Should().Be(120);
        result[1].Mgdl.Should().Be(130);
    }

    [Fact]
    public void Map_EmptyEvents_ReturnsEmpty()
    {
        var result = TandemSourceSensorGlucoseMapper.Map([], Tz);
        result.Should().BeEmpty();
    }
}
