using System.Buffers.Binary;
using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.EventParser;

public class RawEventTests
{
    [Fact]
    public void Parse_ValidEvent_ExtractsHeaderFields()
    {
        var data = new byte[26];
        // source=2 (bits 15-12), eventId=5 (bits 11-0) => 0x2005
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(0), 0x2005);
        // timestamp raw
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(2), 500_000_000);
        // seqNum
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(6), 42);
        // payload (bytes 10-25)
        data[10] = 0xAB;
        data[25] = 0xCD;

        var raw = RawEvent.Parse(data);

        raw.Source.Should().Be(2);
        raw.EventId.Should().Be(5);
        raw.TimestampRaw.Should().Be(500_000_000);
        raw.SeqNum.Should().Be(42u);
        raw.Payload.Length.Should().Be(16);
        raw.Payload.Span[0].Should().Be(0xAB);
        raw.Payload.Span[15].Should().Be(0xCD);
    }

    [Fact]
    public void Parse_ThrowsForShortData()
    {
        var data = new byte[10];
        var act = () => RawEvent.Parse(data);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_MultipleConcatenatedEvents()
    {
        var data = new byte[52]; // 2 events
        // Event 1: eventId=3
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(0), 0x1003);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(2), 100);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(6), 1);

        // Event 2: eventId=5
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(26), 0x2005);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(28), 200);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(32), 2);

        var events = new List<RawEvent>();
        for (var offset = 0; offset + RawEvent.EventLength <= data.Length; offset += RawEvent.EventLength)
            events.Add(RawEvent.Parse(data.AsSpan(offset, RawEvent.EventLength)));

        events.Should().HaveCount(2);
        events[0].EventId.Should().Be(3);
        events[0].SeqNum.Should().Be(1u);
        events[1].EventId.Should().Be(5);
        events[1].SeqNum.Should().Be(2u);
    }
}
