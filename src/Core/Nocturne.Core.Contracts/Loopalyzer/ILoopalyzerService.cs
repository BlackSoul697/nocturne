using Nocturne.Core.Models.Loopalyzer;

namespace Nocturne.Core.Contracts.Loopalyzer;

/// <summary>
/// Loopalyzer report data: per-day 5-min binned arrays for SGV / scheduled basal /
/// temp basal / IOB / COB plus meal markers, predictions (single-day), DIA, and
/// active profiles across a tenant-supplied date range.
/// </summary>
public interface ILoopalyzerService
{
    Task<LoopalyzerResponse> GetDataAsync(LoopalyzerRequest request, CancellationToken ct);

    Task<LoopalyzerAvailability> GetAvailabilityAsync(CancellationToken ct);
}
