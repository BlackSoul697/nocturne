namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// The system clock, displaced by an offset that a test sets for a scope. Outside a
/// <see cref="Shift"/> it reads real time, so a host-wide service built on it behaves as on the
/// system clock for every other test sharing the host.
/// </summary>
public sealed class OffsetClock : TimeProvider
{
    private long _offsetTicks;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() =>
        TimeProvider.System.GetUtcNow().AddTicks(Interlocked.Read(ref _offsetTicks));

    /// <summary>Reads <paramref name="offset"/> from real time until the returned scope is disposed.</summary>
    public IDisposable Shift(TimeSpan offset)
    {
        Interlocked.Exchange(ref _offsetTicks, offset.Ticks);
        return new Restore(this);
    }

    private sealed class Restore(OffsetClock clock) : IDisposable
    {
        public void Dispose() => Interlocked.Exchange(ref clock._offsetTicks, 0);
    }
}
