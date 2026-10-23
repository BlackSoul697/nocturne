using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nocturne.API.Services.Alerts.Conditions;

/// <summary>
/// JSON options for the condition models (<see cref="Nocturne.Core.Models.ConditionNode"/> and its
/// payloads). Matches the shape used by the controllers and Zod schemas: snake_case property
/// naming, case-insensitive read.
/// </summary>
internal static class ConditionJson
{
    public static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };
        // System enums can't carry [JsonConverter] attributes; register the
        // string converter here so leaves like DayOfWeekCondition accept day
        // names ("monday") on the wire instead of forcing integer indices.
        // The converter is bidirectional — integer payloads still parse.
        options.Converters.Add(new JsonStringEnumConverter<DayOfWeek>());
        return options;
    }
}
