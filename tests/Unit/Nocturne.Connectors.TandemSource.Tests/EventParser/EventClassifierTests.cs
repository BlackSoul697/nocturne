using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Models;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.EventParser;

public class EventClassifierTests
{
    private static ParsedEvent CreateEvent(string name) => new()
    {
        EventId = 1,
        EventName = name,
        Source = 0,
        TimestampRaw = 100,
        SeqNum = 1,
        Payload = new byte[16]
    };

    [Theory]
    [InlineData("LID_BASAL_DELIVERY", EventClass.Basal)]
    [InlineData("LID_BOLUS_COMPLETED", EventClass.Bolus)]
    [InlineData("LID_BOLUS_REQUESTED_MSG1", EventClass.Bolus)]
    [InlineData("LID_PUMPING_SUSPENDED", EventClass.BasalSuspension)]
    [InlineData("LID_PUMPING_RESUMED", EventClass.BasalResume)]
    [InlineData("LID_ALARM_ACTIVATED", EventClass.Alarm)]
    [InlineData("LID_CARTRIDGE_FILLED", EventClass.Cartridge)]
    [InlineData("LID_CGM_ALERT_ACTIVATED", EventClass.CgmAlert)]
    [InlineData("LID_CGM_DATA_GXB", EventClass.CgmReading)]
    [InlineData("LID_CGM_START_SESSION_GX", EventClass.CgmStartJoinStop)]
    [InlineData("LID_AA_USER_MODE_CHANGE", EventClass.UserMode)]
    public void Classify_KnownEvents_ReturnsCorrectClass(string eventName, EventClass expected)
    {
        var evt = CreateEvent(eventName);
        EventClassifier.Classify(evt).Should().Be(expected);
    }

    [Fact]
    public void Classify_UnknownEvent_ReturnsNull()
    {
        var evt = CreateEvent("UNKNOWN_EVENT_XYZ");
        EventClassifier.Classify(evt).Should().BeNull();
    }

    [Fact]
    public void ClassifyAll_GroupsEventsByClass()
    {
        var events = new[]
        {
            CreateEvent("LID_BASAL_DELIVERY"),
            CreateEvent("LID_BASAL_DELIVERY"),
            CreateEvent("LID_BOLUS_COMPLETED"),
            CreateEvent("UNKNOWN_EVENT"),
        };

        var result = EventClassifier.ClassifyAll(events);

        result.Should().ContainKey(EventClass.Basal);
        result[EventClass.Basal].Should().HaveCount(2);
        result.Should().ContainKey(EventClass.Bolus);
        result[EventClass.Bolus].Should().HaveCount(1);
        result.Should().NotContainKey(EventClass.Alarm);
    }
}
