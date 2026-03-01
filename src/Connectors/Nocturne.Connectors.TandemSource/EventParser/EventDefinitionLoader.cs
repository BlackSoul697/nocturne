using System.Reflection;
using System.Text.Json;

namespace Nocturne.Connectors.TandemSource.EventParser;

/// <summary>
/// Loads event schemas from the embedded EventDefinitions.json resource.
/// Maps event IDs to their name and payload field layout.
/// </summary>
public static class EventDefinitionLoader
{
    private static readonly Lazy<Dictionary<int, EventDefinition>> Definitions = new(Load);

    public static IReadOnlyDictionary<int, EventDefinition> GetDefinitions() => Definitions.Value;

    public static EventDefinition? GetDefinition(int eventId) =>
        Definitions.Value.GetValueOrDefault(eventId);

    private static Dictionary<int, EventDefinition> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("EventDefinitions.json"))
            ?? throw new InvalidOperationException("EventDefinitions.json embedded resource not found");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var doc = JsonDocument.Parse(stream);

        var result = new Dictionary<int, EventDefinition>();
        var eventsElement = doc.RootElement.GetProperty("events");

        foreach (var eventEntry in eventsElement.EnumerateObject())
        {
            if (!int.TryParse(eventEntry.Name, out var eventId))
                continue;

            var name = eventEntry.Value.GetProperty("name").GetString() ?? $"EVENT_{eventId}";
            var fields = new Dictionary<string, EventFieldDefinition>(StringComparer.OrdinalIgnoreCase);

            if (eventEntry.Value.TryGetProperty("data", out var dataElement))
            {
                foreach (var field in dataElement.EnumerateObject())
                {
                    var fieldDef = new EventFieldDefinition
                    {
                        Type = field.Value.GetProperty("type").GetString() ?? "uint8",
                        Offset = field.Value.GetProperty("offset").GetInt32(),
                        Uom = field.Value.TryGetProperty("uom", out var uom) ? uom.GetString() : null
                    };
                    fields[field.Name] = fieldDef;
                }
            }

            result[eventId] = new EventDefinition { Name = name, Fields = fields };
        }

        return result;
    }

    /// <summary>
    /// Parses a base64-encoded pump event blob into a list of ParsedEvents.
    /// </summary>
    public static List<ParsedEvent> ParseEvents(string base64Data)
    {
        var bytes = Convert.FromBase64String(base64Data);
        var events = new List<ParsedEvent>(bytes.Length / RawEvent.EventLength);

        for (var offset = 0; offset + RawEvent.EventLength <= bytes.Length; offset += RawEvent.EventLength)
        {
            var raw = RawEvent.Parse(bytes.AsSpan(offset, RawEvent.EventLength));
            var definition = GetDefinition(raw.EventId);
            events.Add(ParsedEvent.FromRaw(raw, definition));
        }

        return events;
    }
}
