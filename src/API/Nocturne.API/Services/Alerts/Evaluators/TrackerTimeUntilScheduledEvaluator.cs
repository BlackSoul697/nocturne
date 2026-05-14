using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;

namespace Nocturne.API.Services.Alerts.Evaluators;

/// <summary>
/// Evaluates hours until (or since) a tracker instance's ScheduledAt.
/// Positive values mean the scheduled time is in the future; negative means past due.
/// Returns false when no matching active snapshot or no ScheduledAt.
/// </summary>
public class TrackerTimeUntilScheduledEvaluator : IConditionEvaluator
{
    private readonly TimeProvider _timeProvider;

    public TrackerTimeUntilScheduledEvaluator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AlertConditionType ConditionType => AlertConditionType.TrackerTimeUntilScheduled;

    public Task<bool> EvaluateAsync(string conditionParamsJson, SensorContext context, CancellationToken ct)
    {
        if (context.TrackerSnapshots is null)
            return Task.FromResult(false);

        var condition = JsonSerializer.Deserialize<TrackerTimeUntilScheduledCondition>(conditionParamsJson, EvaluatorJson.Options);
        if (condition is null)
            return Task.FromResult(false);

        var snapshot = context.TrackerSnapshots.FirstOrDefault(s => s.DefinitionId == condition.DefinitionId);
        if (snapshot is null || !snapshot.IsActive || snapshot.ScheduledAt is null)
            return Task.FromResult(false);

        var now = _timeProvider.GetUtcNow();
        var hoursUntil = (decimal)(snapshot.ScheduledAt.Value - now).TotalHours;

        return Task.FromResult(ComparisonOps.Compare(hoursUntil, condition.Operator, condition.Hours));
    }
}
