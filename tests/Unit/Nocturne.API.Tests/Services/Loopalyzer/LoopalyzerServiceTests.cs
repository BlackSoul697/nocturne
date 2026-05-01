using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.API.Services.Treatments;
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

        var timeline = Mock.Of<ITherapyTimelineResolver>();
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
        var treatments = Mock.Of<ITreatmentService>();
        var iob = Mock.Of<IIobService>();
        var cob = Mock.Of<ICobService>();
        return new LoopalyzerService(options, service, timeline, tempBasals, apsRepo, treatments, iob, cob);
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
    public async Task GetData_AllowsRangeAtCap()
    {
        var sut = CreateService(maxDays: 14);

        var response = await sut.GetDataAsync(
            new LoopalyzerRequest { From = "2026-01-01", To = "2026-01-14" },
            CancellationToken.None);

        response.Days.Should().BeEmpty();
    }
}
