using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;

namespace Nocturne.API.Services.Alerts.Evaluators;

/// <summary>
/// Evaluates hours since a tracker instance started. Returns false when no matching
/// active snapshot exists or StartedAt is null.
/// </summary>
public class TrackerAgeEvaluator : IConditionEvaluator
{
    private readonly TimeProvider _timeProvider;

    public TrackerAgeEvaluator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AlertConditionType ConditionType => AlertConditionType.TrackerAge;

    public Task<bool> EvaluateAsync(string conditionParamsJson, SensorContext context, CancellationToken ct)
    {
        if (context.TrackerSnapshots is null)
            return Task.FromResult(false);

        var condition = JsonSerializer.Deserialize<TrackerAgeCondition>(conditionParamsJson, EvaluatorJson.Options);
        if (condition is null)
            return Task.FromResult(false);

        var snapshot = context.TrackerSnapshots.FirstOrDefault(s => s.DefinitionId == condition.DefinitionId);
        if (snapshot is null || !snapshot.IsActive || snapshot.StartedAt is null)
            return Task.FromResult(false);

        var now = _timeProvider.GetUtcNow();
        var ageHours = (decimal)(now - snapshot.StartedAt.Value).TotalHours;

        return Task.FromResult(ComparisonOps.Compare(ageHours, condition.Operator, condition.Hours));
    }
}
