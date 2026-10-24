using FluentAssertions;
using Xunit;

namespace Nocturne.Widget.Contracts.Tests;

public class AlarmHubEventsTests
{
    [Theory]
    [InlineData("alarm", false, false)]
    [InlineData("urgent_alarm", true, false)]
    [InlineData("clear_alarm", false, true)]
    [InlineData("engine_alarm", false, false)]
    [InlineData("engine_urgent_alarm", true, false)]
    [InlineData("engine_clear_alarm", false, true)]
    public void Each_event_maps_to_its_alarm_kind(string eventName, bool urgent, bool cleared)
    {
        AlarmHubEvents.Kinds.Should().ContainKey(eventName);
        AlarmHubEvents.Kinds[eventName].Should().Be(new AlarmEventKind(urgent, cleared));
    }

    [Fact]
    public void Only_the_six_alarm_events_are_listened_for()
    {
        AlarmHubEvents.Kinds.Should().HaveCount(6);
    }
}
