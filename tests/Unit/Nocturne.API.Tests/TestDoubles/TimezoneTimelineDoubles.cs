using Moq;
using Nocturne.Core.Contracts.Timezones;
using Nocturne.Core.Models.Timezones;

namespace Nocturne.API.Tests.TestDoubles;

internal static class TimezoneTimelineDoubles
{
    /// <summary>A timeline with no entries: every instant resolves to its UTC date.</summary>
    public static ITimezoneTimelineService Utc() => In();

    /// <summary>A timeline whose one entry puts every instant in <paramref name="ianaZone"/>.</summary>
    public static ITimezoneTimelineService In(string? ianaZone = null)
    {
        var entries = ianaZone is null
            ? []
            : new List<TimezoneTimelineEntry>
            {
                new() { Id = Guid.NewGuid(), EffectiveFrom = DateTime.MinValue, Timezone = ianaZone },
            };
        var service = new Mock<ITimezoneTimelineService>();
        service
            .Setup(s => s.GetResolverAsync(It.IsAny<double?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimezoneTimeline(entries));
        return service.Object;
    }
}
