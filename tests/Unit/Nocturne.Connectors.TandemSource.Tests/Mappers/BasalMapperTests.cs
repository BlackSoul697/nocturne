using System.Buffers.Binary;
using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Mappers;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.Mappers;

public class BasalMapperTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Utc;

    private static ParsedEvent CreateBasalEvent(uint timestamp, uint seqNum,
        ushort commandedRate, ushort profileRate, ushort rateSource)
    {
        var payload = new byte[16];
        var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0), commandedRate);
        fields["Commanded Rate"] = new EventFieldDefinition { Type = "uint16", Offset = 0 };

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2), profileRate);
        fields["Profile Basal Rate"] = new EventFieldDefinition { Type = "uint16", Offset = 2 };

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(4), rateSource);
        fields["Commanded Rate Source"] = new EventFieldDefinition { Type = "uint16", Offset = 4 };

        return new ParsedEvent
        {
            EventId = 5,
            EventName = "LID_BASAL_DELIVERY",
            Source = 0,
            TimestampRaw = timestamp,
            SeqNum = seqNum,
            Payload = payload,
            FieldDefinitions = fields
        };
    }

    [Fact]
    public void Map_TwoConsecutiveEvents_CalculatesDuration()
    {
        var events = new List<ParsedEvent>
        {
            CreateBasalEvent(1000, 1, commandedRate: 1500, profileRate: 1000, rateSource: 1),
            CreateBasalEvent(1300, 2, commandedRate: 2000, profileRate: 1000, rateSource: 3)
        };

        var result = TandemSourceBasalMapper.Map(events, Tz);

        result.Should().HaveCount(2);
        result[0].Rate.Should().Be(1.5); // 1500 / 1000
        result[0].ScheduledRate.Should().Be(1.0); // 1000 / 1000
        result[0].Origin.Should().Be(TempBasalOrigin.Scheduled);
        (result[0].EndTimestamp - result[0].StartTimestamp).Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public void Map_AlgorithmSource_SetsAlgorithmOrigin()
    {
        var events = new List<ParsedEvent>
        {
            CreateBasalEvent(1000, 1, commandedRate: 500, profileRate: 1000, rateSource: 3),
        };

        var result = TandemSourceBasalMapper.Map(events, Tz);
        result[0].Origin.Should().Be(TempBasalOrigin.Algorithm);
    }

    [Fact]
    public void Map_SuspendedSource_SetsSuspendedOrigin()
    {
        var events = new List<ParsedEvent>
        {
            CreateBasalEvent(1000, 1, commandedRate: 0, profileRate: 1000, rateSource: 0),
        };

        var result = TandemSourceBasalMapper.Map(events, Tz);
        result[0].Origin.Should().Be(TempBasalOrigin.Suspended);
        result[0].Rate.Should().Be(0);
    }

    [Fact]
    public void Map_EmptyEvents_ReturnsEmpty()
    {
        var result = TandemSourceBasalMapper.Map([], Tz);
        result.Should().BeEmpty();
    }
}
