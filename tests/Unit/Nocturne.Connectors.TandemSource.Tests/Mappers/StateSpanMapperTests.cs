using System.Buffers.Binary;
using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Mappers;
using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.Mappers;

public class StateSpanMapperTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Utc;

    private static ParsedEvent CreateSuspendEvent(uint timestamp, uint seqNum, byte suspendReason)
    {
        var payload = new byte[16];
        payload[5] = suspendReason; // SuspendReason at offset 5 per EventDefinitions
        var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["SuspendReason"] = new EventFieldDefinition { Type = "uint8", Offset = 5 }
        };
        return new ParsedEvent
        {
            EventId = 11,
            EventName = "LID_PUMPING_SUSPENDED",
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = payload,
            FieldDefinitions = fields
        };
    }

    private static ParsedEvent CreateResumeEvent(uint timestamp, uint seqNum)
    {
        return new ParsedEvent
        {
            EventId = 12,
            EventName = "LID_PUMPING_RESUMED",
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = new byte[16],
            FieldDefinitions = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ParsedEvent CreateUserModeEvent(uint timestamp, uint seqNum, byte requestedAction)
    {
        var payload = new byte[16];
        payload[1] = requestedAction; // RequestedAction at offset 1
        var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["RequestedAction"] = new EventFieldDefinition { Type = "uint8", Offset = 1 }
        };
        return new ParsedEvent
        {
            EventId = 229,
            EventName = "LID_AA_USER_MODE_CHANGE",
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = payload,
            FieldDefinitions = fields
        };
    }

    [Fact]
    public void Map_SuspendWithResume_CreatesStateSpanWithEndTime()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.BasalSuspension] = [CreateSuspendEvent(1000, 1, 0)],
            [EventClass.BasalResume] = [CreateResumeEvent(1300, 2)]
        };

        var result = TandemSourceStateSpanMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].State.Should().Be("Suspended");
        result[0].Category.Should().Be(StateSpanCategory.PumpMode);
        result[0].EndTimestamp.Should().NotBeNull();
        result[0].Metadata.Should().ContainKey("reason").WhoseValue.Should().Be("User Suspended");
    }

    [Theory]
    [InlineData((byte)0, "User Suspended")]
    [InlineData((byte)1, "Alarm")]
    [InlineData((byte)2, "Malfunction")]
    [InlineData((byte)6, "Auto Suspend (PLGS)")]
    public void Map_SuspendReason_MapsToExpectedReason(byte reasonRaw, string expectedReason)
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.BasalSuspension] = [CreateSuspendEvent(1000, 1, reasonRaw)]
        };

        var result = TandemSourceStateSpanMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].Metadata.Should().ContainKey("reason").WhoseValue.Should().Be(expectedReason);
    }

    [Fact]
    public void Map_SuspendWithoutResume_EndTimeIsNull()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.BasalSuspension] = [CreateSuspendEvent(1000, 1, 0)]
        };

        var result = TandemSourceStateSpanMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].EndTimestamp.Should().BeNull();
    }

    [Fact]
    public void Map_UserModeSleepStartAndStop_CreatesSleepSpan()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.UserMode] =
            [
                CreateUserModeEvent(1000, 1, 1),  // Start Sleep
                CreateUserModeEvent(1300, 2, 2)   // Stop Sleep
            ]
        };

        var result = TandemSourceStateSpanMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].Category.Should().Be(StateSpanCategory.Sleep);
        result[0].State.Should().Be("Sleep");
        result[0].EndTimestamp.Should().NotBeNull();
    }

    [Fact]
    public void Map_UserModeExerciseStartAndStop_CreatesExerciseSpan()
    {
        var classified = new Dictionary<EventClass, List<ParsedEvent>>
        {
            [EventClass.UserMode] =
            [
                CreateUserModeEvent(1000, 1, 3),  // Start Exercise
                CreateUserModeEvent(1120, 2, 4)   // Stop Exercise (20 min)
            ]
        };

        var result = TandemSourceStateSpanMapper.Map(classified, Tz);

        result.Should().HaveCount(1);
        result[0].Category.Should().Be(StateSpanCategory.Exercise);
        result[0].State.Should().Be("Exercise");
        result[0].EndTimestamp.Should().NotBeNull();
    }

    [Fact]
    public void Map_EmptyClassified_ReturnsEmpty()
    {
        var result = TandemSourceStateSpanMapper.Map(new Dictionary<EventClass, List<ParsedEvent>>(), Tz);
        result.Should().BeEmpty();
    }
}
