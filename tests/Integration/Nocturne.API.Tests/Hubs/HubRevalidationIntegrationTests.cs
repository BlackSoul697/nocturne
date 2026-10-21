using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.API.Hubs;
using Nocturne.API.Tests.Integration.Infrastructure;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Hubs;

/// <summary>
/// A hub connection authorized by the credential on its upgrade request stays open only while that
/// credential still authenticates: <see cref="HubConnectionRevalidator"/> replays it through the
/// real authentication pipeline and closes the connection once it no longer does.
/// </summary>
[Trait("Category", "Integration")]
public class HubRevalidationIntegrationTests : ApiIntegrationTestBase
{
    private Guid _tenantId;
    private Guid _subjectId;

    public HubRevalidationIntegrationTests(ApiIntegrationTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        using var client = CreateAuthenticatedClient();
        (await client.GetAsync("/api/v1/status")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var conn = await OpenAsync();
        _tenantId = await AuthTestHelpers.GetTenantIdAsync(conn);
        (_subjectId, _) = await AuthTestHelpers.SeedAuthenticatedSubjectAsync(conn, _tenantId, "Revalidation User");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(await GetPostgresConnectionStringAsync());
        await conn.OpenAsync();
        return conn;
    }

    private async Task<(Guid GrantId, string Secret)> SeedSecretAsync()
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20));
        await using var conn = await OpenAsync();
        return (await AuthTestHelpers.SeedApiSecretGrantAsync(conn, _tenantId, _subjectId, secret), secret);
    }

    private async Task RevokeAsync(Guid grantId)
    {
        await using var conn = await OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenantId, false);";
            cmd.Parameters.AddWithValue("tenantId", _tenantId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE oauth_grants SET revoked_at = now() WHERE id = @id;";
            cmd.Parameters.AddWithValue("id", grantId);
            (await cmd.ExecuteNonQueryAsync()).Should().Be(1);
        }
    }

    private async Task<HubConnection> SubscribedAlertHubAsync(string secret)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(ApiClient.BaseAddress!, "hubs/alerts"), options =>
            {
                options.Headers["X-Forwarded-Host"] = Fixture.TenantHost;
                options.Headers["api-secret"] = secret;
            })
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .Build();
        HubConnections.Add(connection);

        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe");
        return connection;
    }

    private static Task ClosedAsync(HubConnection connection)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        return closed.Task;
    }

    [Fact]
    public async Task A_connection_whose_credential_is_revoked_is_closed_and_a_live_one_stays_open()
    {
        var (revokedGrant, revokedSecret) = await SeedSecretAsync();
        var (_, liveSecret) = await SeedSecretAsync();
        var revoked = await SubscribedAlertHubAsync(revokedSecret);
        var live = await SubscribedAlertHubAsync(liveSecret);
        var revokedClosed = ClosedAsync(revoked);

        await RevokeAsync(revokedGrant);
        await Fixture.Services.GetRequiredService<HubConnectionRevalidator>()
            .RevalidateAsync(CancellationToken.None);

        (await Task.WhenAny(revokedClosed, Task.Delay(TimeSpan.FromSeconds(10))))
            .Should().BeSameAs(revokedClosed, "the revoked credential's connection must be closed");
        live.State.Should().Be(HubConnectionState.Connected);
        await live.InvokeAsync("Subscribe");
    }
}
