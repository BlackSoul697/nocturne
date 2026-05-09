using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Loopalyzer;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class LoopalyzerServiceTests
{
    private static LoopalyzerService CreateService(int maxDays = 14, IEntryService? entryService = null)
    {
        var options = Options.Create(new LoopalyzerOptions { MaxRangeDays = maxDays });
        var service = entryService ?? Mock.Of<IEntryService>(s =>
            s.GetEntriesWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<Entry>>(Array.Empty<Entry>()));

        var snapshot = new TherapySnapshot(
            dia: 5.0, peakMinutes: 75, carbsPerHour: 30,
            timezone: TimeZoneInfo.Utc, ccpPercentage: null, ccpTimeshiftMs: 0,
            sensitivityEntries: null, carbRatioEntries: null, basalEntries: null);
        var timelineMock = new Mock<ITherapyTimelineResolver>();
        timelineMock.Setup(t => t.GetSnapshotAtAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        timelineMock.Setup(t => t.BuildAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long fromMills, long toMills, string? _, CancellationToken _) =>
                new TherapyTimeline(new[] { new TherapySegment(fromMills, toMills, snapshot) }));
        var timeline = timelineMock.Object;
        var tempBasals = Mock.Of<ITempBasalRepository>(r =>
            r.GetAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<TempBasal>>(Array.Empty<TempBasal>()));
        var apsRepo = Mock.Of<IApsSnapshotRepository>(r =>
            r.GetAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<ApsSnapshot>>(Array.Empty<ApsSnapshot>()));
        var iobCalc = Mock.Of<IIobCalculator>(i =>
            i.FromBoluses(It.IsAny<List<Bolus>>(), It.IsAny<long?>())
                == new IobResult { Iob = 0, Activity = 0 });
        var cobCalc = Mock.Of<ICobCalculator>(c =>
            c.FromCarbIntakes(It.IsAny<List<CarbIntake>>(), It.IsAny<List<Bolus>?>(), It.IsAny<List<TempBasal>?>(), It.IsAny<long?>())
                == new CobResult { Cob = 0 });
        var bolusRepo = Mock.Of<IBolusRepository>(r =>
            r.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<BolusKind?>(),
                It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<Bolus>>(Array.Empty<Bolus>()));
        var carbIntakeRepo = Mock.Of<ICarbIntakeRepository>(r =>
            r.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<CarbIntake>>(Array.Empty<CarbIntake>()));
        var deviceEventRepo = Mock.Of<IDeviceEventRepository>(r =>
            r.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()) == Task.FromResult<IEnumerable<DeviceEvent>>(Array.Empty<DeviceEvent>()));
        var activeProfile = Mock.Of<IActiveProfileResolver>();
        var basal = Mock.Of<IBasalScheduleRepository>();
        var sensitivity = Mock.Of<ISensitivityScheduleRepository>();
        var carbRatio = Mock.Of<ICarbRatioScheduleRepository>();
        var targetRange = Mock.Of<ITargetRangeResolver>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = Mock.Of<ITenantAccessor>(t => t.TenantId == Guid.NewGuid());
        return new LoopalyzerService(options, service, timeline, tempBasals, apsRepo,
            iobCalc, cobCalc, bolusRepo, carbIntakeRepo, deviceEventRepo,
            activeProfile, basal, sensitivity, carbRatio, targetRange, cache, tenant);
    }

    [Fact]
    public async Task GetData_ThrowsValidation_WhenRangeExceedsCap()
    {
        var sut = CreateService(maxDays: 14);

        Func<Task> act = () => sut.GetDataAsync(
            new LoopalyzerRequest { From = "2026-01-01", To = "2026-01-20" },
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("*14 days*");
    }

    [Fact]
    public async Task GetData_ThrowsValidation_WhenFromAfterTo()
    {
        var sut = CreateService();

        Func<Task> act = () => sut.GetDataAsync(
            new LoopalyzerRequest { From = "2026-01-10", To = "2026-01-05" },
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("*on or after*");
    }

    [Fact]
    public async Task GetData_ThrowsValidation_WhenFromMalformed()
    {
        var sut = CreateService();

        Func<Task> act = () => sut.GetDataAsync(
            new LoopalyzerRequest { From = "2026/01/01", To = "2026-01-02" },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetData_CachesPerDay_BetweenCalls()
    {
        // Spy on entry service to count calls (one per day per fetch).
        var entryMock = new Mock<IEntryService>();
        entryMock.Setup(s => s.GetEntriesWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Entry>());

        var sut = CreateService(entryService: entryMock.Object);

        // 3-day range, called twice — second call should hit the cache.
        await sut.GetDataAsync(new LoopalyzerRequest { From = "2026-01-01", To = "2026-01-03" }, CancellationToken.None);
        await sut.GetDataAsync(new LoopalyzerRequest { From = "2026-01-01", To = "2026-01-03" }, CancellationToken.None);

        // Without cache: 6 calls (3 days × 2 invocations). With cache: 3 calls (only first batch).
        entryMock.Verify(s => s.GetEntriesWithAdvancedFilterAsync(
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task GetData_AllowsRangeAtCap()
    {
        var sut = CreateService(maxDays: 14);

        var response = await sut.GetDataAsync(
            new LoopalyzerRequest { From = "2026-01-01", To = "2026-01-14" },
            CancellationToken.None);

        response.Days.Should().HaveCount(14);
        response.Timezone.Should().Be(TimeZoneInfo.Utc.Id);
        response.Days.Select(d => d.Date).Should().BeInAscendingOrder();
        response.Days[0].Sgv.Should().HaveCount(288);
    }
}
