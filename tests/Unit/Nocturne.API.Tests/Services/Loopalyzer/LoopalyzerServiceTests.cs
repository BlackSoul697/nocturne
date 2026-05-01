using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Models.Loopalyzer;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class LoopalyzerServiceTests
{
    private static LoopalyzerService CreateService(int maxDays = 14)
    {
        var options = Options.Create(new LoopalyzerOptions { MaxRangeDays = maxDays });
        return new LoopalyzerService(options);
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
