using System.Buffers.Binary;

namespace Nocturne.Connectors.TandemSource.EventParser;

/// <summary>
/// Represents the fixed 26-byte header of a Tandem pump event.
/// Layout (big-endian):
///   uint16 @ 0:  source (top 4 bits) + event ID (bottom 12 bits)
///   uint32 @ 2:  timestamp (seconds since Tandem Epoch)
///   uint32 @ 6:  sequence number
///   16 bytes @ 10: payload
/// </summary>
public readonly struct RawEvent
{
    public const int EventLength = 26;
    public const int PayloadOffset = 10;
    public const int PayloadLength = 16;

    public int Source { get; init; }
    public int EventId { get; init; }
    public uint TimestampRaw { get; init; }
    public uint SeqNum { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }

    public static RawEvent Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < EventLength)
            throw new ArgumentException($"Data must be at least {EventLength} bytes, got {data.Length}");

        var sourceAndId = BinaryPrimitives.ReadUInt16BigEndian(data);
        var timestampRaw = BinaryPrimitives.ReadUInt32BigEndian(data[2..]);
        var seqNum = BinaryPrimitives.ReadUInt32BigEndian(data[6..]);

        return new RawEvent
        {
            Source = (sourceAndId & 0xF000) >> 12,
            EventId = sourceAndId & 0x0FFF,
            TimestampRaw = timestampRaw,
            SeqNum = seqNum,
            Payload = data[PayloadOffset..EventLength].ToArray()
        };
    }

    public DateTime GetUtcTimestamp(TimeZoneInfo timezone) =>
        TandemEpoch.ToUtcDateTime(TimestampRaw, timezone);
}
