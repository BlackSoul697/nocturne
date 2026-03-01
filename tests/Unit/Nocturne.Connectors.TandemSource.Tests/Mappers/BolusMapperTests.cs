using System.Buffers.Binary;
using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Mappers;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.Mappers;

public class BolusMapperTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Utc;

    private static ParsedEvent CreateBolusEvent(string name, uint timestamp, uint seqNum, ushort bolusId,
        float insulinDelivered = 0, float insulinRequested = 0, byte options = 0)
    {
        var payload = new byte[16];

        var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        // BolusID at offset 0 (uint16)
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), bolusId);
        fields["BolusID"] = new EventFieldDefinition { Type = "uint16", Offset = 0 };

        if (name == "LID_BOLUS_COMPLETED")
        {
            BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(8), insulinDelivered);
            fields["InsulinDelivered"] = new EventFieldDefinition { Type = "float32", Offset = 8 };
            BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(12), insulinRequested);
            fields["InsulinRequested"] = new EventFieldDefinition { Type = "float32", Offset = 12 };
        }

        if (name == "LID_BOLUS_REQUESTED_MSG2")
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), bolusId);
            fields["BolusID"] = new EventFieldDefinition { Type = "uint16", Offset = 2 };
            payload[1] = options;
            fields["Options"] = new EventFieldDefinition { Type = "uint8", Offset = 1 };
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(6), 0);
            fields["Duration"] = new EventFieldDefinition { Type = "uint16", Offset = 6 };
        }

        return new ParsedEvent
        {
            EventId = 1,
            EventName = name,
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = payload,
            FieldDefinitions = fields
        };
    }

    [Fact]
    public void Map_SingleCompletedBolus_CreatesBolus()
    {
        var events = new List<ParsedEvent>
        {
            CreateBolusEvent("LID_BOLUS_COMPLETED", 1000, 10, bolusId: 1,
                insulinDelivered: 2.5f, insulinRequested: 3.0f)
        };

        var result = TandemSourceBolusMapper.Map(events, Tz);

        result.Should().HaveCount(1);
        result[0].Delivered.Should().BeApproximately(2.5, 0.01);
        result[0].Programmed.Should().BeApproximately(3.0, 0.01);
        result[0].BolusType.Should().Be(BolusType.Normal);
        result[0].Kind.Should().Be(BolusKind.Manual);
        result[0].Automatic.Should().BeFalse();
    }

    [Fact]
    public void Map_BolusWithMsg2Options3_IsAutomatic()
    {
        var events = new List<ParsedEvent>
        {
            CreateBolusEvent("LID_BOLUS_REQUESTED_MSG2", 999, 9, bolusId: 1, options: 3),
            CreateBolusEvent("LID_BOLUS_COMPLETED", 1000, 10, bolusId: 1,
                insulinDelivered: 0.05f, insulinRequested: 0.05f)
        };

        var result = TandemSourceBolusMapper.Map(events, Tz);

        result.Should().HaveCount(1);
        result[0].Automatic.Should().BeTrue();
        result[0].Kind.Should().Be(BolusKind.Algorithm);
    }

    [Fact]
    public void Map_MultipleBolusIds_GroupsSeparately()
    {
        var events = new List<ParsedEvent>
        {
            CreateBolusEvent("LID_BOLUS_COMPLETED", 1000, 10, bolusId: 1,
                insulinDelivered: 1.0f, insulinRequested: 1.0f),
            CreateBolusEvent("LID_BOLUS_COMPLETED", 2000, 20, bolusId: 2,
                insulinDelivered: 2.0f, insulinRequested: 2.0f)
        };

        var result = TandemSourceBolusMapper.Map(events, Tz);

        result.Should().HaveCount(2);
        result[0].Delivered.Should().BeApproximately(1.0, 0.01);
        result[1].Delivered.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public void Map_EmptyEvents_ReturnsEmpty()
    {
        var result = TandemSourceBolusMapper.Map([], Tz);
        result.Should().BeEmpty();
    }
}
