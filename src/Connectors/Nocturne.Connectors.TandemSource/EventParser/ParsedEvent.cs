using System.Buffers.Binary;

namespace Nocturne.Connectors.TandemSource.EventParser;

/// <summary>
/// A pump event with its raw header parsed and payload fields accessible by name.
/// Field extraction is driven by the event schema from EventDefinitions.json.
/// </summary>
public class ParsedEvent
{
    public int EventId { get; init; }
    public string EventName { get; init; } = "Unknown";
    public int Source { get; init; }
    public uint TimestampRaw { get; init; }
    public uint SeqNum { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
    public Dictionary<string, EventFieldDefinition>? FieldDefinitions { get; init; }

    private TimeZoneInfo? _timezone;

    public void SetTimezone(TimeZoneInfo tz) => _timezone = tz;

    public DateTime GetUtcTimestamp() =>
        _timezone != null
            ? TandemEpoch.ToUtcDateTime(TimestampRaw, _timezone)
            : TandemEpoch.EpochDateTimeOffset.AddSeconds(TimestampRaw).UtcDateTime;

    public DateTime GetUtcTimestamp(TimeZoneInfo timezone) =>
        TandemEpoch.ToUtcDateTime(TimestampRaw, timezone);

    /// <summary>
    /// Gets the UTC timestamp for a secondary timestamp field (e.g. EGV TimeStamp in CGM events)
    /// </summary>
    public DateTime GetFieldUtcTimestamp(string fieldName, TimeZoneInfo timezone)
    {
        var raw = GetUInt32(fieldName);
        return TandemEpoch.ToUtcDateTime(raw, timezone);
    }

    public byte GetUInt8(string fieldName)
    {
        var offset = GetFieldOffset(fieldName);
        return Payload.Span[offset];
    }

    public sbyte GetInt8(string fieldName)
    {
        var offset = GetFieldOffset(fieldName);
        return (sbyte)Payload.Span[offset];
    }

    public ushort GetUInt16(string fieldName)
    {
        var offset = GetFieldOffset(fieldName);
        return BinaryPrimitives.ReadUInt16BigEndian(Payload.Span[offset..]);
    }

    public short GetInt16(string fieldName)
    {
        var offset = GetFieldOffset(fieldName);
        return BinaryPrimitives.ReadInt16BigEndian(Payload.Span[offset..]);
    }

    public uint GetUInt32(string fieldName)
    {
        var offset = GetFieldOffset(fieldName);
        return BinaryPrimitives.ReadUInt32BigEndian(Payload.Span[offset..]);
    }

    public float GetFloat32(string fieldName)
    {
        var offset = GetFieldOffset(fieldName);
        return BinaryPrimitives.ReadSingleBigEndian(Payload.Span[offset..]);
    }

    public bool HasField(string fieldName) =>
        FieldDefinitions?.ContainsKey(fieldName) == true;

    private int GetFieldOffset(string fieldName)
    {
        if (FieldDefinitions == null || !FieldDefinitions.TryGetValue(fieldName, out var def))
            throw new InvalidOperationException($"Field '{fieldName}' not found in event {EventName} (ID {EventId})");
        return def.Offset;
    }

    public static ParsedEvent FromRaw(RawEvent raw, EventDefinition? definition)
    {
        return new ParsedEvent
        {
            EventId = raw.EventId,
            EventName = definition?.Name ?? $"Unknown_{raw.EventId}",
            Source = raw.Source,
            TimestampRaw = raw.TimestampRaw,
            SeqNum = raw.SeqNum,
            Payload = raw.Payload,
            FieldDefinitions = definition?.Fields
        };
    }
}

public class EventDefinition
{
    public string Name { get; set; } = default!;
    public Dictionary<string, EventFieldDefinition> Fields { get; set; } = new();
}

public class EventFieldDefinition
{
    public string Type { get; set; } = default!;
    public int Offset { get; set; }
    public string? Uom { get; set; }
}
