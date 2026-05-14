using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;

namespace Nocturne.API.Services.Alerts.Evaluators;

/// <summary>
/// Evaluates whether a tracker instance exists and matches the expected active state.
/// Returns false when <see cref="SensorContext.TrackerSnapshots"/> is null or contains
/// no matching definition.
/// </summary>
public class TrackerActiveEvaluator : IConditionEvaluator
{
    public AlertConditionType ConditionType => AlertConditionType.TrackerActive;

    public Task<bool> EvaluateAsync(string conditionParamsJson, SensorContext context, CancellationToken ct)
    {
        if (context.TrackerSnapshots is null)
            return Task.FromResult(false);

        var condition = JsonSerializer.Deserialize<TrackerActiveCondition>(conditionParamsJson, EvaluatorJson.Options);
        if (condition is null)
            return Task.FromResult(false);

        var snapshot = context.TrackerSnapshots.FirstOrDefault(s => s.DefinitionId == condition.DefinitionId);
        if (snapshot is null)
            return Task.FromResult(false);

        return Task.FromResult(snapshot.IsActive == condition.ExpectedActive);
    }
}
