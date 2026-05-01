using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts.Loopalyzer;
using Nocturne.Core.Models.Loopalyzer;

namespace Nocturne.API.Services.Loopalyzer;

public sealed class LoopalyzerService : ILoopalyzerService
{
    private readonly LoopalyzerOptions _options;

    public LoopalyzerService(IOptions<LoopalyzerOptions> options)
    {
        _options = options.Value;
    }

    public Task<LoopalyzerResponse> GetDataAsync(LoopalyzerRequest request, CancellationToken ct)
    {
        var (_, _) = ParseAndValidateRange(request);

        // Phase-1 stub: subsequent tasks fill in per-day binning, profiles, and DIA resolution.
        var response = new LoopalyzerResponse(
            Days: Array.Empty<LoopalyzerDay>(),
            Profiles: Array.Empty<LoopalyzerProfile>(),
            Timezone: "UTC",
            MostRecentDia: null,
            MostRecentBgLow: null,
            MostRecentBgHigh: null
        );
        return Task.FromResult(response);
    }

    public Task<LoopalyzerAvailability> GetAvailabilityAsync(CancellationToken ct)
        => Task.FromResult(new LoopalyzerAvailability(HasApsData: false, LatestApsAt: null));

    private (DateOnly From, DateOnly To) ParseAndValidateRange(LoopalyzerRequest request)
    {
        if (!DateOnly.TryParseExact(request.From, "yyyy-MM-dd", out var from))
            throw new ValidationException("From must be YYYY-MM-DD");
        if (!DateOnly.TryParseExact(request.To, "yyyy-MM-dd", out var to))
            throw new ValidationException("To must be YYYY-MM-DD");
        if (to < from)
            throw new ValidationException("To must be on or after From");
        if (to.DayNumber - from.DayNumber + 1 > _options.MaxRangeDays)
            throw new ValidationException($"Range cannot exceed {_options.MaxRangeDays} days");
        return (from, to);
    }
}
