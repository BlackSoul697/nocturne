using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services.Profiles.Resolvers;
using Nocturne.API.Tests.Infrastructure;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services.Profiles.Resolvers;

/// <summary>
/// <see cref="ActiveProfileResolver"/> and <see cref="BasalSegmentService"/> read a Profile span
/// through one parser (<see cref="ProfileSpanMetadata"/>), so an AAPS switch named after its
/// customization runs the same stored profile, percentage and shift in both.
/// </summary>
public sealed class ProfileSpanResolverAgreementTests : IDisposable
{
    private const long From = 1705320000000;
    private const long To = From + 3_600_000;

    private readonly Mock<IStateSpanService> _stateSpans = new();
    private readonly Mock<IBasalScheduleRepository> _schedules = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public ProfileSpanResolverAgreementTests()
    {
        _schedules
            .Setup(r => r.GetByProfileNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _schedules
            .Setup(r => r.GetByProfileNameAsync("Rhys", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new BasalSchedule
                {
                    ProfileName = "Rhys",
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(From - 86_400_000).UtcDateTime,
                    Entries = [new ScheduleEntry { Time = "00:00", TimeAsSeconds = 0, Value = 0.8 }],
                },
            ]);
    }

    public void Dispose() => _cache.Dispose();

    public static TheoryData<string, Dictionary<string, object>> AapsSwitches => new()
    {
        {
            "as written now",
            new Dictionary<string, object>
            {
                ["profileName"] = "Rhys (110%,1h)",
                ["originalProfileName"] = "Rhys",
                ["percentage"] = 110.0,
                ["timeshift"] = 1.0,
                ["uploadedTimeshift"] = 3_600_000.0,
                ["timeshiftUnit"] = "ms",
                ["enteredBy"] = "openaps://AndroidAPS",
            }
        },
        {
            "as written before the unit was kept",
            new Dictionary<string, object>
            {
                ["profileName"] = "Rhys (110%,1h)",
                ["originalProfileName"] = "Rhys",
                ["percentage"] = 110.0,
                ["timeshift"] = 3_600_000.0,
                ["enteredBy"] = "openaps://AndroidAPS",
            }
        },
    };

    private void SetupSpan(Dictionary<string, object> metadata) =>
        _stateSpans
            .Setup(s => s.GetStateSpansAsync(
                StateSpanCategory.Profile,
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new StateSpan
                {
                    Id = Guid.NewGuid().ToString(),
                    Category = StateSpanCategory.Profile,
                    State = "Active",
                    StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(From).UtcDateTime,
                    Metadata = metadata,
                },
            ]);

    [Theory]
    [MemberData(nameof(AapsSwitches))]
    public async Task Both_resolve_an_AAPS_switch_to_the_profile_it_customizes(
        string _, Dictionary<string, object> metadata)
    {
        SetupSpan(metadata);
        var active = new ActiveProfileResolver(
            _stateSpans.Object, TestTenantCache.Create(_cache), NullLogger<ActiveProfileResolver>.Instance);
        var basal = new BasalSegmentService(
            _stateSpans.Object, _schedules.Object, Mock.Of<ITherapySettingsRepository>(), TestTenantCache.Create(_cache));

        var name = await active.GetActiveProfileNameAsync(From + 60_000);
        var adjustment = await active.GetCircadianAdjustmentAsync(From + 60_000);
        var segments = new List<Nocturne.Core.Models.Basal.BasalSegment>();
        await foreach (var segment in basal.GetSegmentsAsync(From, To))
            segments.Add(segment);

        name.Should().Be("Rhys");
        adjustment.Should().Be(new CircadianAdjustment(110, 3_600_000));
        segments.Should().NotBeEmpty();
        segments.Should().OnlyContain(s => s.ProfileName == "Rhys",
            "the switch is named after its customization, which no stored schedule is");
        segments.Should().OnlyContain(s => Math.Abs(s.ScheduledUnitsPerHour - 0.8) < 1e-9);
        segments.Should().OnlyContain(s => Math.Abs(s.UnitsPerHour - 0.88) < 1e-9);
    }
}
