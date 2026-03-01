using System.Buffers.Binary;
using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.EventParser;

public class EventDefinitionLoaderTests
{
    [Fact]
    public void GetDefinitions_ContainsBaseEvents()
    {
        var defs = EventDefinitionLoader.GetDefinitions();
        defs.Should().ContainKey(3);
        defs[3].Name.Should().Be("LID_BASAL_RATE_CHANGE");
        defs.Should().ContainKey(20);
        defs[20].Name.Should().Be("LID_BOLUS_COMPLETED");
        defs.Should().ContainKey(256);
        defs[256].Name.Should().Be("LID_CGM_DATA_GXB");
        defs.Should().ContainKey(279);
        defs[279].Name.Should().Be("LID_BASAL_DELIVERY");
    }

    [Fact]
    public void GetDefinitions_ContainsCustomEvents_FromTconnectsyncCustomEventsJson()
    {
        var defs = EventDefinitionLoader.GetDefinitions();
        defs.Should().ContainKey(36);
        defs[36].Name.Should().Be("LID_USB_CONNECTED");
        defs.Should().ContainKey(37);
        defs[37].Name.Should().Be("LID_USB_DISCONNECTED");
        defs.Should().ContainKey(48);
        defs[48].Name.Should().Be("LID_CARBS_ENTERED");
        defs.Should().ContainKey(81);
        defs[81].Name.Should().Be("LID_DAILY_BASAL");
    }

    [Fact]
    public void GetDefinition_UnknownId_ReturnsNull()
    {
        EventDefinitionLoader.GetDefinition(99999).Should().BeNull();
    }

    [Fact]
    public void ParseEvents_EmptyBase64_ReturnsEmptyList()
    {
        var result = EventDefinitionLoader.ParseEvents(Convert.ToBase64String(Array.Empty<byte>()));
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseEvents_SingleEvent_ReturnsOneParsedEvent()
    {
        var raw = new byte[26];
        BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(0), 90); // eventId 90 = LID_NEW_DAY
        BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(2), 1000);
        BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(6), 1);
        var base64 = Convert.ToBase64String(raw);

        var result = EventDefinitionLoader.ParseEvents(base64);

        result.Should().HaveCount(1);
        result[0].EventId.Should().Be(90);
        result[0].EventName.Should().Be("LID_NEW_DAY");
        result[0].TimestampRaw.Should().Be(1000u);
        result[0].SeqNum.Should().Be(1u);
    }

    [Fact]
    public void ParseEvents_TwoEvents_ReturnsTwoParsedEvents()
    {
        var raw = new byte[52];
        BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(0), 11);
        BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(2), 1000);
        BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt16BigEndian(raw.AsSpan(26), 12);
        BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(28), 2000);
        BinaryPrimitives.WriteUInt32BigEndian(raw.AsSpan(32), 2);
        var base64 = Convert.ToBase64String(raw);

        var result = EventDefinitionLoader.ParseEvents(base64);

        result.Should().HaveCount(2);
        result[0].EventName.Should().Be("LID_PUMPING_SUSPENDED");
        result[0].SeqNum.Should().Be(1u);
        result[1].EventName.Should().Be("LID_PUMPING_RESUMED");
        result[1].SeqNum.Should().Be(2u);
    }
}
