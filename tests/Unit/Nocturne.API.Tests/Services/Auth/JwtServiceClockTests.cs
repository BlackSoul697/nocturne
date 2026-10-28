using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Models.Configuration;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

/// <summary>
/// <see cref="JwtService"/> issues and validates tokens against its injected clock. The clock starts
/// years before the test runs, so a token stamped or checked against the system clock fails every case.
/// </summary>
[Trait("Category", "Unit")]
public class JwtServiceClockTests
{
    private static readonly DateTimeOffset Minted = new(2020, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly FakeTimeProvider _clock = new(Minted);
    private readonly JwtService _jwt;
    private readonly string _token;

    public JwtServiceClockTests()
    {
        _jwt = Service(_clock);
        _token = _jwt.GenerateAccessToken(
            new SubjectInfo { Id = Guid.CreateVersion7() }, [], [], [], lifetime: Lifetime);
    }

    private static JwtService Service(TimeProvider clock) => new(
        Options.Create(new JwtOptions
        {
            SecretKey = "jwt-clock-test-secret-key-at-least-32-chars",
            Issuer = "nocturne",
            Audience = "nocturne-api",
        }),
        NullLogger<JwtService>.Instance,
        clock);

    /// <summary>A validator whose clock reads <paramref name="now"/>, which may precede <see cref="Minted"/>.</summary>
    private static JwtService ValidatorAt(DateTimeOffset now) => Service(new FakeTimeProvider(now));

    [Fact]
    public void The_token_is_stamped_with_the_injected_clock()
    {
        var result = _jwt.ValidateAccessToken(_token);

        result.IsValid.Should().BeTrue(result.Error);
        result.Claims!.IssuedAt.Should().Be(Minted);
        result.Claims.ExpiresAt.Should().Be(Minted + Lifetime);
    }

    [Fact]
    public void The_session_token_is_stamped_with_the_injected_clock()
    {
        var token = _jwt.GenerateAccessToken(
            new SubjectInfo { Id = Guid.CreateVersion7() }, [], [], sessionId: "family", lifetime: Lifetime);

        var result = _jwt.ValidateAccessToken(token);

        result.IsValid.Should().BeTrue(result.Error);
        result.Claims!.IssuedAt.Should().Be(Minted);
        result.Claims.ExpiresAt.Should().Be(Minted + Lifetime);
    }

    [Fact]
    public void A_token_is_accepted_up_to_its_expiry_plus_the_clock_skew()
    {
        _clock.Advance(Lifetime + JwtService.ClockSkew);

        _jwt.ValidateAccessToken(_token).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_token_past_its_expiry_plus_the_clock_skew_is_expired()
    {
        _clock.Advance(Lifetime + JwtService.ClockSkew + TimeSpan.FromSeconds(1));

        var result = _jwt.ValidateAccessToken(_token);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(JwtValidationError.Expired);
    }

    [Fact]
    public void A_token_is_accepted_from_the_clock_skew_before_it_was_issued()
    {
        ValidatorAt(Minted - JwtService.ClockSkew).ValidateAccessToken(_token).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_token_earlier_than_the_clock_skew_before_it_was_issued_is_not_yet_valid()
    {
        var result = ValidatorAt(Minted - JwtService.ClockSkew - TimeSpan.FromSeconds(1))
            .ValidateAccessToken(_token);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(JwtValidationError.NotYetValid);
    }
}
