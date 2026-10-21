using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Services;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Analytics;

/// <summary>
/// <c>GET /api/v4/loop-analysis/daily-profile</c> end to end on PostgreSQL: a week of algorithm
/// temp basals at 1.5 U/h against a 1.0 U/h schedule in Sydney, compared with the week before.
/// </summary>
[Trait("Category", "Integration")]
public class LoopAnalysisDailyProfileIntegrationTests(ApiIntegrationTestFixture fixture, ITestOutputHelper output)
    : LoopAnalysisIntegrationTestBase(fixture, output)
{
    private const string Path = "/api/v4/loop-analysis/daily-profile";

    // Midnight 2024-06-10 in Sydney (UTC+10, no DST in June).
    private static readonly DateTime WeekStart = new(2024, 6, 9, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekEnd = WeekStart.AddDays(7);
    private static readonly DateTime PreviousWeekStart = WeekStart.AddDays(-7);

    private static string Query(DateTime from, DateTime to, DateTime? compareFrom = null, DateTime? compareTo = null) =>
        $"{Path}?from={from:O}&to={to:O}"
        + (compareFrom is { } cf ? $"&compareFrom={cf:O}&compareTo={compareTo:O}" : "");

    [Fact]
    public async Task Returns_the_primary_and_the_adjacent_comparison()
    {
        await SeedProfileAsync("Australia/Sydney");
        await SeedWeekAsync(PreviousWeekStart, tempRate: 0.5);
        await SeedWeekAsync(WeekStart, tempRate: 1.5);
        await SeedTemporaryTargetsAsync(WeekStart);

        var body = await GetOkAsync(AuthenticatedClient, Query(WeekStart, WeekEnd, PreviousWeekStart, WeekStart));

        body.GetProperty("historyClamped").GetBoolean().Should().BeFalse();
        Absent(body, "timeZoneUnavailableReason").Should().BeTrue();

        var primary = body.GetProperty("primary");
        primary.GetProperty("slots").GetArrayLength().Should().Be(288);
        primary.GetProperty("days").GetArrayLength().Should().Be(7);
        primary.GetProperty("profileNames").EnumerateArray().Select(n => n.GetString()).Should().Equal("Default");

        var ten = primary.GetProperty("slots")[120];
        ten.GetProperty("glucose").GetProperty("p50").GetDouble().Should().Be(120);
        ten.GetProperty("scheduledBasal").GetProperty("p50").GetDouble().Should().Be(1.0);
        ten.GetProperty("deliveredBasal").GetProperty("p50").GetDouble().Should().Be(1.5);
        ten.GetProperty("deliveredBasal").GetProperty("n").GetInt32().Should().Be(7);
        ten.GetProperty("netLoopCorrection").GetProperty("mean").GetDouble().Should().Be(0.5);
        ten.GetProperty("targetLow").GetDouble().Should().Be(144.146, "an 8 mmol/L temporary target ran at 10:00 every day");
        primary.GetProperty("slots")[132].GetProperty("targetLow").GetDouble().Should().Be(90);
        primary.GetProperty("summary").GetProperty("daysWithData").GetInt32().Should().Be(7);
        body.GetProperty("ignoredFilters").GetArrayLength().Should().Be(0);

        var three = primary.GetProperty("slots")[36];
        three.GetProperty("netLoopCorrection").GetProperty("automaticBolusMean").GetDouble()
            .Should().Be(2.4, "0.2 U over five minutes every day");
        primary.GetProperty("slots")[96].GetProperty("manualBoluses").GetProperty("meanPerDay").GetDouble().Should().Be(3);

        primary.GetProperty("summary").GetProperty("deliveredToScheduledRatio").GetDouble().Should().Be(1.5);
        primary.GetProperty("summary").GetProperty("netLoopCorrectionPerDay").GetDouble().Should().Be(12.2);

        var comparison = body.GetProperty("comparison");
        comparison.GetProperty("slots")[120].GetProperty("netLoopCorrection").GetProperty("mean").GetDouble().Should().Be(-0.5);
        comparison.GetProperty("days").GetArrayLength().Should().Be(7);
    }

    [Fact]
    public async Task Fetches_a_comparison_that_does_not_touch_the_primary_on_its_own()
    {
        await SeedProfileAsync("Australia/Sydney");
        var twoWeeksBack = PreviousWeekStart.AddDays(-7);
        await SeedWeekAsync(twoWeeksBack, tempRate: 0.5);
        await SeedWeekAsync(WeekStart, tempRate: 1.5);

        var body = await GetOkAsync(AuthenticatedClient, Query(WeekStart, WeekEnd, twoWeeksBack, PreviousWeekStart));

        body.GetProperty("primary").GetProperty("slots")[120].GetProperty("netLoopCorrection")
            .GetProperty("mean").GetDouble().Should().Be(0.5);
        var comparison = body.GetProperty("comparison");
        comparison.GetProperty("slots")[120].GetProperty("netLoopCorrection").GetProperty("mean").GetDouble().Should().Be(-0.5);
        comparison.GetProperty("summary").GetProperty("daysWithData").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Rejects_a_range_longer_than_ninety_days()
    {
        var response = await AuthenticatedClient.GetAsync(Query(WeekStart.AddDays(-91), WeekStart));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var comparison = await AuthenticatedClient.GetAsync(Query(WeekStart, WeekEnd, WeekStart.AddDays(-100), WeekStart));
        comparison.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(new[] { Scope.ReportsRead, Scope.GlucoseRead, Scope.TreatmentsRead }, true, true, false)]
    [InlineData(new[] { Scope.ReportsRead, Scope.TreatmentsRead, Scope.TherapyRead, Scope.DevicesRead }, false, true, true)]
    [InlineData(new[] { Scope.ReportsRead, Scope.GlucoseRead, Scope.TherapyRead }, true, false, true)]
    public async Task Narrows_each_category_independently(string[] scopes, bool glucose, bool treatments, bool therapy)
    {
        await SeedProfileAsync("Australia/Sydney");
        await SeedWeekAsync(WeekStart, tempRate: 1.5);
        using var client = await CreateTokenClientAsync(scopes);

        var body = await GetOkAsync(client, Query(WeekStart, WeekEnd) + "&excludeIllness=true");

        var ten = body.GetProperty("primary").GetProperty("slots")[120];
        Absent(ten, "glucose").Should().Be(!glucose);
        Absent(ten, "deliveredBasal").Should().Be(!treatments);
        Absent(ten, "iob").Should().Be(!treatments);
        Absent(ten, "scheduledBasal").Should().Be(!(therapy && treatments));
        Absent(ten, "targetLow").Should().Be(!(therapy && treatments));
        var ignored = body.GetProperty("ignoredFilters").EnumerateArray().Select(f => f.GetString()).ToList();
        ignored.Should().Equal(treatments ? [] : ["excludeIllness"], "only a parameter the caller sent is reported");
    }

    [Fact]
    public async Task A_history_clamped_member_is_analysed_over_its_last_24_hours_only()
    {
        await SeedProfileAsync("Australia/Sydney");
        using var clamped = await CreateHistoryClampedClientAsync();
        var now = DateTime.UtcNow;

        var body = await GetOkAsync(clamped, Query(now.AddDays(-7), now));

        body.GetProperty("historyClamped").GetBoolean().Should().BeTrue();
        var from = body.GetProperty("primary").GetProperty("from").GetDateTime().ToUniversalTime();
        from.Should().BeCloseTo(now.AddHours(-24), TimeSpan.FromMinutes(5));
        body.GetProperty("primary").GetProperty("days").GetArrayLength().Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task A_share_gets_its_reason_and_no_slots()
    {
        await SeedProfileAsync("Australia/Sydney");
        await SeedWeekAsync(WeekStart, tempRate: 1.5);
        using var share = await CreateShareClientAsync();

        var body = await GetOkAsync(share, Query(WeekStart, WeekEnd));

        body.GetProperty("timeZoneUnavailableReason").GetString().Should().Be("Share");
        body.GetProperty("primary").GetProperty("slots").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Narrows_the_response_to_the_callers_scopes()
    {
        await SeedProfileAsync("Australia/Sydney");
        await SeedWeekAsync(WeekStart, tempRate: 1.5);
        using var glucoseOnly = await CreateTokenClientAsync(Scope.ReportsRead, Scope.GlucoseRead);

        var body = await GetOkAsync(glucoseOnly, Query(WeekStart, WeekEnd));

        var primary = body.GetProperty("primary");
        var ten = primary.GetProperty("slots")[120];
        ten.GetProperty("glucose").GetProperty("p50").GetDouble().Should().Be(120);
        Absent(ten, "deliveredBasal").Should().BeTrue();
        Absent(ten, "netLoopCorrection").Should().BeTrue();
        Absent(ten, "iob").Should().BeTrue();
        Absent(ten, "manualBoluses").Should().BeTrue();
        Absent(ten, "scheduledBasal").Should().BeTrue();
        Absent(ten, "targetLow").Should().BeTrue();
        primary.GetProperty("profileNames").GetArrayLength().Should().Be(0);
        primary.GetProperty("hourlyNetLoopCorrection").GetArrayLength().Should().Be(0);
        Absent(primary.GetProperty("days")[0], "totalInsulin").Should().BeTrue();
    }

    [Fact]
    public async Task Returns_no_slots_without_a_tenant_timezone()
    {
        await SeedWeekAsync(WeekStart, tempRate: 1.5);
        await SeedTemporaryTargetsAsync(WeekStart);

        var body = await GetOkAsync(AuthenticatedClient, Query(WeekStart, WeekEnd, PreviousWeekStart, WeekStart));

        body.GetProperty("timeZoneUnavailableReason").GetString().Should().Be("NotConfigured");
        body.GetProperty("primary").GetProperty("slots").GetArrayLength().Should().Be(0);
        body.GetProperty("primary").GetProperty("days").GetArrayLength().Should().Be(0);
        body.GetProperty("comparison").GetProperty("slots").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// Seven days from <paramref name="start"/>: glucose at 120 and an algorithm temp at
    /// <paramref name="tempRate"/> every five minutes, a 0.2 U automatic bolus at local 03:00 and a
    /// 3 U manual bolus at local 08:00.
    /// </summary>
    private async Task SeedWeekAsync(DateTime start, double tempRate)
    {
        using var scope = Fixture.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessor>().SetTenant(
            new TenantContext(Fixture.TenantId, "loop-analysis", "Loop analysis", true, false));
        await using var context = await scope.ServiceProvider.GetRequiredService<INocturneDbContextFactory>().CreateAsync();

        for (var t = start; t < start.AddDays(7); t = t.AddMinutes(5))
        {
            context.SensorGlucose.Add(new SensorGlucoseEntity { Id = Guid.NewGuid(), Timestamp = t, Mgdl = 120, DataSource = "test" });
            context.TempBasals.Add(new TempBasalEntity
            {
                Id = Guid.NewGuid(), Timestamp = t, EndTimestamp = t.AddMinutes(30), Rate = tempRate,
                Origin = "Algorithm", Device = "pump", DataSource = "test",
            });
        }
        for (var day = 0; day < 7; day++)
        {
            var midnight = start.AddDays(day);
            context.Boluses.Add(new BolusEntity { Id = Guid.NewGuid(), Timestamp = midnight.AddHours(3), Insulin = 0.2, BolusKind = "Algorithm", DataSource = "test" });
            context.Boluses.Add(new BolusEntity { Id = Guid.NewGuid(), Timestamp = midnight.AddHours(8), Insulin = 3, BolusKind = "Manual", DataSource = "test" });
        }
        await context.SaveChangesAsync();
    }

    /// <summary>A 30-minute 8 mmol/L temporary target at local 10:00 on each of seven days.</summary>
    private async Task SeedTemporaryTargetsAsync(DateTime start)
    {
        var targets = Enumerable.Range(0, 7).Select(day => new
        {
            eventType = "Temporary Target",
            targetTop = 8.0,
            targetBottom = 8.0,
            units = "mmol",
            duration = 30,
            created_at = start.AddDays(day).AddHours(10).ToString("O"),
        });
        var response = await AuthenticatedClient.PostAsJsonAsync("/api/v1/treatments", targets);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }
}
