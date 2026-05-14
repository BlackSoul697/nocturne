using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;

namespace Nocturne.API.Services.Alerts.Evaluators;

/// <summary>
/// Evaluates hours remaining until a tracker instance's lifespan expires. Returns false
/// when no matching active snapshot exists or StartedAt or LifespanHours is null.
/// </summary>
public class TrackerRemainingEvaluator : IConditionEvaluator
{
    private readonly TimeProvider _timeProvider;

    public TrackerRemainingEvaluator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AlertConditionType ConditionType => AlertConditionType.TrackerRemaining;

    public Task<bool> EvaluateAsync(string conditionParamsJson, SensorContext context, CancellationToken ct)
    {
        if (context.TrackerSnapshots is null)
            return Task.FromResult(false);

        var condition = JsonSerializer.Deserialize<TrackerRemainingCondition>(conditionParamsJson, EvaluatorJson.Options);
        if (condition is null)
            return Task.FromResult(false);

        var snapshot = context.TrackerSnapshots.FirstOrDefault(s => s.DefinitionId == condition.DefinitionId);
        if (snapshot is null || !snapshot.IsActive || snapshot.StartedAt is null || snapshot.LifespanHours is null)
            return Task.FromResult(false);

        var now = _timeProvider.GetUtcNow();
        var ageHours = (decimal)(now - snapshot.StartedAt.Value).TotalHours;
        var remainingHours = snapshot.LifespanHours.Value - ageHours;

        return Task.FromResult(ComparisonOps.Compare(remainingHours, condition.Operator, condition.Hours));
    }
}
