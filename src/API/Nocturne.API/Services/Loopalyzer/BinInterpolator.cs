namespace Nocturne.API.Services.Loopalyzer;

/// <summary>
/// In-place linear interpolation across short null runs in a 5-minute-bin array.
/// Ports the legacy Loopalyzer heuristic: rising series (end &gt;= start) bridge
/// short gaps liberally, but a sharp rise (end/start &gt; ratio) is gated to a
/// tighter cap. Falling series are always allowed up to the wider cap.
/// </summary>
internal static class BinInterpolator
{
    /// <summary>
    /// Mutates <paramref name="bins"/>: for each maximal run of consecutive nulls
    /// flanked by non-null endpoints, fill linearly when the gap meets the
    /// rising/falling thresholds. Trailing or leading nulls (no flank) are left as-is.
    /// </summary>
    public static void Interpolate(double?[] bins, int risingGap, int fallingGap, double ratio)
    {
        var i = 0;
        while (i < bins.Length)
        {
            if (bins[i] is not double start)
            {
                i++;
                continue;
            }

            // Find the next non-null after a run of nulls.
            var j = i + 1;
            while (j < bins.Length && bins[j] is null)
                j++;

            if (j >= bins.Length)
                break; // trailing nulls

            var gap = j - i - 1;
            if (gap == 0)
            {
                i = j;
                continue;
            }

            var end = bins[j]!.Value;
            var rising = end >= start;
            var allowed = false;
            if (rising)
            {
                if (gap <= risingGap)
                    allowed = true;
                else if (gap <= fallingGap && (start <= 0 || end / start <= ratio))
                    allowed = true;
            }
            else
            {
                allowed = gap <= fallingGap;
            }

            if (allowed)
            {
                var step = (end - start) / (gap + 1);
                for (var k = 1; k <= gap; k++)
                    bins[i + k] = start + step * k;
            }

            i = j;
        }
    }
}
