using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Models;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.API.Services.Analytics;

internal sealed partial class LoopAnalysisService
{
    private static readonly DeviceEventType[] SensorStartTypes = [DeviceEventType.SensorStart, DeviceEventType.SensorChange];

    /// <summary>
    /// How far past the window either side each load reads, so an anchor at the edge of a range has
    /// the carbs either side of it that decide a correction bolus or a rise, and the stable stretch
    /// before a rise.
    /// </summary>
    private const long EventMarginMs = LocalSlotBinning.HourMs;

    public async Task<EventResponseReport> GetEventResponseAsync(
        LoopAnalysisFilter filter, EventResponseDefinition definition, TimeZoneInfo zone, CancellationToken ct = default)
    {
        var window = EventWindow.Of(definition);
        var sensorStarts = definition.Cohorts.Any(c => c.Anchor == EventAnchorKind.SensorStart);
        var primaryFrom = Window.Of(filter.From, filter.To).From;
        var (primary, comparison) = await ForRangesAsync(
            filter,
            range => LoadEventsAsync(range, window, zone, sensorStarts, ct),
            (inputs, range) => EventResponseCalculator.Calculate(
                inputs, Mask(filter, range, zone, inputs.Sources), range.From, range.To, definition,
                listEvents: range.From == primaryFrom));
        return new EventResponseReport { Primary = primary, Comparison = comparison };
    }

    /// <summary>
    /// The Daily profile's reads over <paramref name="range"/> widened by the window and
    /// <see cref="EventMarginMs"/>, since an anchor near either end reads past it, and the sensor
    /// starts in the range when a cohort asks for them, from
    /// <see cref="EventAnchors.SensorSessionMergeMs"/> before it so a start just inside the range is
    /// merged with the change recorded just before it.
    /// </summary>
    private async Task<EventResponseInputs> LoadEventsAsync(
        Window range, EventWindow window, TimeZoneInfo zone, bool sensorStarts, CancellationToken ct)
    {
        var padded = new Window(range.From - window.BeforeMs - EventMarginMs, range.To + window.AfterMs + EventMarginMs);
        var sensorTask = sensorStarts
            ? deviceEventRepository.GetAsync(
                ToUtc(range.From - EventAnchors.SensorSessionMergeMs), ToUtc(range.To), null, null, int.MaxValue, descending: false, ct: ct)
            : Task.FromResult(Enumerable.Empty<Core.Models.V4.DeviceEvent>());
        var loaded = await LoadAsync(padded, zone, ct);

        return new EventResponseInputs
        {
            Series = loaded.Inputs,
            Sources = loaded.Sources,
            SensorStarts = [.. (await sensorTask).Where(e => SensorStartTypes.Contains(e.EventType)).Select(e => e.Mills)],
        };
    }
}
