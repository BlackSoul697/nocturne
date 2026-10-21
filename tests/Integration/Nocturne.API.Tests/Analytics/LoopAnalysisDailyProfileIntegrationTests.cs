using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Services;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Analytics;

/// <summary>
/// <c>GET /api/v4/loop-analysis/daily-profile</c> end to end on PostgreSQL: a week of algorithm
/// temp basals at 1.5 U/h against a 1.0 U/h schedule in Sydney, compared with the week before.
/// </summary>
[Trait("Category", "Integration")]
public class LoopAnalysisDailyProfileIntegrationTests(ApiIntegrationTestFixture fixture, ITestOutputHelper output)
    : ApiIntegrationTestBase(fixture, output)
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
        ten.GetProperty("targetLow").GetProperty("p50").GetDouble().Should().Be(90);

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
    public async Task Rejects_a_range_longer_than_ninety_days()
    {
        var response = await AuthenticatedClient.GetAsync(Query(WeekStart.AddDays(-91), WeekStart));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var comparison = await AuthenticatedClient.GetAsync(Query(WeekStart, WeekEnd, WeekStart.AddDays(-100), WeekStart));
        comparison.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

        var body = await GetOkAsync(AuthenticatedClient, Query(WeekStart, WeekEnd, PreviousWeekStart, WeekStart));

        body.GetProperty("timeZoneUnavailableReason").GetString().Should().Be("NotConfigured");
        body.GetProperty("primary").GetProperty("slots").GetArrayLength().Should().Be(0);
        body.GetProperty("primary").GetProperty("days").GetArrayLength().Should().Be(0);
        body.GetProperty("comparison").GetProperty("slots").GetArrayLength().Should().Be(0);
    }

    private async Task SeedProfileAsync(string timezone)
    {
        static object[] Values(double value) => [new { time = "00:00", value }];
        var document = new
        {
            defaultProfile = "Default",
            startDate = "2024-01-01T00:00:00.000Z",
            mills = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            units = "mg/dl",
            store = new Dictionary<string, object>
            {
                ["Default"] = new
                {
                    dia = 4, timezone, units = "mg/dl",
                    basal = Values(1.0),
                    carbratio = Values(10),
                    sens = Values(50),
                    target_low = Values(90),
                    target_high = Values(140),
                },
            },
        };
        var response = await AuthenticatedClient.PostAsJsonAsync("/api/v1/profile", document);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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

    private async Task<HttpClient> CreateTokenClientAsync(params string[] scopes)
    {
        await using var conn = new NpgsqlConnection(await GetPostgresConnectionStringAsync());
        await conn.OpenAsync();
        var tenantId = await AuthTestHelpers.GetTenantIdAsync(conn);
        var (subjectId, _) = await AuthTestHelpers.SeedAuthenticatedSubjectAsync(conn, tenantId, "Loop analysis member");
        var accessToken = await AuthTestHelpers.IssueSessionTokenAsync(Fixture, subjectId);

        using var member = AuthTestHelpers.CreateAuthenticatedSubjectClient(Fixture, accessToken);
        var response = await member.PostAsJsonAsync("/api/auth/direct-grants", new { label = "loop-analysis", scopes });
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        var token = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("token").GetString()!;
        return AuthTestHelpers.CreateBearerClient(Fixture, token);
    }

    private static async Task<JsonElement> GetOkAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static bool Absent(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;
}
