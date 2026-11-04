using System.Diagnostics;
using System.Globalization;

namespace OptimizationXY.Charger.Services.Acquisition;

// =============================================================================
// FramePairing - which head frame goes with which tail frame.
// -----------------------------------------------------------------------------
// Pairing head frame N with tail frame N (capture order) is wrong whenever one
// camera delivers an extra or a missing frame: every pair after it is one frame
// apart (about 4 degrees of rotation at ~90 frames per turn). The Rebuild
// program saw a whole scan paired 18 degrees apart when one camera got two
// extra frames at ScanStart (F-02, F-45, F-47).
//
// Ported from Rebuild.Core FramePairing / ScanTiming:
//   - Encoder (pulse) mode: both cameras fire on the same encoder pulse, so the
//     difference of their frame counters is constant. That difference is found
//     from the time-aligned pairs (median) and frames are paired on
//     tail number = head number + offset.
//   - Free-run (frame rate) mode: pair by arrival time after removing the
//     systematic tail-minus-head delivery delay; tolerance 0.6 x frame period.
//   - Either way, if far fewer pairs are found than the frame counts allow
//     (fewer than max(8, half)), the timing data is not trusted and capture
//     order is used, as before.
// This changes only which two images are joined. The joining, the angle
// assignment, the XY search and the virtual peel are unchanged.
// =============================================================================

/// <summary>One head/tail frame pair, as indexes into the head and tail lists.</summary>
public readonly record struct FramePair(int HeadIndex, int TailIndex);

/// <summary>The result of pairing one scan's frames.</summary>
public sealed class FramePairingResult
{
    /// <summary>The pairs in capture order.</summary>
    public List<FramePair> Pairs { get; } = [];

    /// <summary>Head / tail frames with no partner (not used in the scan).</summary>
    public List<int> UnpairedHead { get; } = [];
    public List<int> UnpairedTail { get; } = [];

    /// <summary>"order", "time" or "pulse", with a note when a fallback was taken.</summary>
    public string Method { get; set; } = "order";

    /// <summary>Typical tail-minus-head arrival delay (Stopwatch ticks).</summary>
    public long SystematicOffsetTicks { get; set; }

    /// <summary>Pulse method: tail frame number minus head frame number.</summary>
    public long NumberOffset { get; set; }

    /// <summary>One line for the log.</summary>
    public string Describe()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        string s = $"{Pairs.Count} pairs by {Method}";

        if (Method.StartsWith("time", StringComparison.Ordinal) || Method.StartsWith("pulse", StringComparison.Ordinal))
            s += $", tail-head {(SystematicOffsetTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", ci)} ms";

        if (Method.StartsWith("pulse", StringComparison.Ordinal))
            s += $", frame number offset {NumberOffset.ToString(ci)}";

        return s + $"; unpaired head {UnpairedHead.Count}, tail {UnpairedTail.Count}";
    }
}

/// <summary>Pure pairing functions over arrival ticks and frame counters, so every case can be tested.</summary>
public static class FramePairing
{
    /// <summary>Fewer pairs than this always means the timing pairing is not trusted.</summary>
    public const int MinimumPairs = 8;

    /// <summary>
    /// A tail frame about half a period away from two heads goes to the later
    /// head: both fire on one pulse and the tail is delivered after the head.
    /// </summary>
    public const double ForwardPreference = 0.3;

    /// <summary>Capture-order pairing (the earlier behaviour): position i with position i.</summary>
    public static FramePairingResult PairByOrder(int headCount, int tailCount)
    {
        var r = new FramePairingResult { Method = "order" };
        int n = Math.Min(headCount, tailCount);

        for (int i = 0; i < n; i++)
            r.Pairs.Add(new FramePair(i, i));

        for (int i = n; i < headCount; i++) r.UnpairedHead.Add(i);
        for (int i = n; i < tailCount; i++) r.UnpairedTail.Add(i);

        return r;
    }

    /// <summary>
    /// Pairs one scan: by encoder pulse (frame counters) when
    /// <paramref name="pulseTriggered"/>, otherwise by arrival time, falling back
    /// to capture order when too few pairs are found. Both lists must be in
    /// capture order. Ticks are <see cref="Stopwatch"/> ticks.
    /// </summary>
    public static FramePairingResult Pair(
        IReadOnlyList<long> headTicks,
        IReadOnlyList<long> tailTicks,
        IReadOnlyList<long> headNumbers,
        IReadOnlyList<long> tailNumbers,
        bool pulseTriggered)
    {
        ArgumentNullException.ThrowIfNull(headTicks);
        ArgumentNullException.ThrowIfNull(tailTicks);
        ArgumentNullException.ThrowIfNull(headNumbers);
        ArgumentNullException.ThrowIfNull(tailNumbers);

        int possible = Math.Min(headTicks.Count, tailTicks.Count);

        if (possible == 0 ||
            headTicks.Any(t => t <= 0) ||
            tailTicks.Any(t => t <= 0))
        {
            FramePairingResult none = PairByOrder(headTicks.Count, tailTicks.Count);
            if (possible > 0)
                none.Method = "order (no capture times)";
            return none;
        }

        long tolerance = DefaultTolerance(headTicks);

        FramePairingResult pairing = pulseTriggered
            ? PairByPulse(headNumbers, tailNumbers, headTicks, tailTicks, tolerance)
            : PairByTime(headTicks, tailTicks, tolerance);

        if (pairing.Pairs.Count < Math.Max(MinimumPairs, possible / 2))
        {
            FramePairingResult order = PairByOrder(headTicks.Count, tailTicks.Count);
            order.Method = $"order (fallback: {pairing.Method} found only {pairing.Pairs.Count} pairs)";
            return order;
        }

        return pairing;
    }

    /// <summary>0.6 x the median head frame interval; long.MaxValue when it cannot be measured.</summary>
    public static long DefaultTolerance(IReadOnlyList<long> headTicks)
    {
        double period = MedianInterval(headTicks);
        return double.IsNaN(period) || period <= 0 ? long.MaxValue : (long)(0.6 * period);
    }

    /// <summary>Pairs by arrival time after removing the systematic tail-minus-head delay.</summary>
    public static FramePairingResult PairByTime(IReadOnlyList<long> headTicks, IReadOnlyList<long> tailTicks, long toleranceTicks)
    {
        var r = new FramePairingResult
        {
            Method = "time",
            SystematicOffsetTicks = EstimateOffsetTicks(headTicks, tailTicks)
        };

        long offset = r.SystematicOffsetTicks;
        int i = 0, j = 0;

        while (i < headTicks.Count && j < tailTicks.Count)
        {
            long dt = tailTicks[j] - offset - headTicks[i];

            if (Math.Abs(dt) <= toleranceTicks)
            {
                // The next tail is even closer to this head: this tail has no partner.
                if (j + 1 < tailTicks.Count && Math.Abs(tailTicks[j + 1] - offset - headTicks[i]) < Math.Abs(dt))
                {
                    r.UnpairedTail.Add(j++);
                    continue;
                }

                // This tail is even closer to the next head: this head has no partner.
                if (i + 1 < headTicks.Count && Math.Abs(tailTicks[j] - offset - headTicks[i + 1]) < Math.Abs(dt))
                {
                    r.UnpairedHead.Add(i++);
                    continue;
                }

                r.Pairs.Add(new FramePair(i++, j++));
            }
            else if (dt < 0)
            {
                r.UnpairedTail.Add(j++);
            }
            else
            {
                r.UnpairedHead.Add(i++);
            }
        }

        for (; i < headTicks.Count; i++) r.UnpairedHead.Add(i);
        for (; j < tailTicks.Count; j++) r.UnpairedTail.Add(j);

        return r;
    }

    /// <summary>
    /// Encoder mode: pairs on tail number = head number + offset, the offset
    /// being the median counter difference of the time-aligned pairs. Falls
    /// back to the time pairing when the counters cannot be aligned.
    /// </summary>
    public static FramePairingResult PairByPulse(
        IReadOnlyList<long> headNumbers,
        IReadOnlyList<long> tailNumbers,
        IReadOnlyList<long> headTicks,
        IReadOnlyList<long> tailTicks,
        long toleranceTicks)
    {
        FramePairingResult time = PairByTime(headTicks, tailTicks, toleranceTicks);

        if (time.Pairs.Count == 0)
        {
            time.Method = "time (no alignment for pulse pairing)";
            return time;
        }

        if (headNumbers.Count != headTicks.Count ||
            tailNumbers.Count != tailTicks.Count ||
            !IsStrictlyIncreasing(headNumbers) ||
            !IsStrictlyIncreasing(tailNumbers))
        {
            time.Method = "time (frame numbers not usable)";
            return time;
        }

        long numberOffset = (long)Math.Round(Median(
            time.Pairs.Select(p => (double)(tailNumbers[p.TailIndex] - headNumbers[p.HeadIndex])).ToList()));

        var r = new FramePairingResult
        {
            Method = "pulse",
            SystematicOffsetTicks = time.SystematicOffsetTicks,
            NumberOffset = numberOffset
        };

        int i = 0, j = 0;

        while (i < headNumbers.Count && j < tailNumbers.Count)
        {
            long d = tailNumbers[j] - numberOffset - headNumbers[i];

            if (d == 0)
                r.Pairs.Add(new FramePair(i++, j++));
            else if (d < 0)
                r.UnpairedTail.Add(j++);
            else
                r.UnpairedHead.Add(i++);
        }

        for (; i < headNumbers.Count; i++) r.UnpairedHead.Add(i);
        for (; j < tailNumbers.Count; j++) r.UnpairedTail.Add(j);

        return r;
    }

    /// <summary>
    /// The systematic tail-minus-head arrival difference: the median nearest
    /// neighbour difference when it is well inside the frame period, otherwise
    /// the forward median unless the backward one is clearly nearer.
    /// </summary>
    public static long EstimateOffsetTicks(IReadOnlyList<long> headTicks, IReadOnlyList<long> tailTicks)
    {
        if (headTicks.Count == 0 || tailTicks.Count == 0)
            return 0;

        var forward = new List<double>(headTicks.Count);
        var backward = new List<double>(headTicks.Count);
        var nearest = new List<double>(headTicks.Count);
        int j = 0;

        for (int i = 0; i < headTicks.Count; i++)
        {
            while (j < tailTicks.Count && tailTicks[j] < headTicks[i])
                j++;

            bool hasForward = j < tailTicks.Count;
            bool hasBackward = j > 0;
            double fwd = hasForward ? tailTicks[j] - headTicks[i] : double.NaN;
            double back = hasBackward ? tailTicks[j - 1] - headTicks[i] : double.NaN;

            if (hasForward) forward.Add(fwd);
            if (hasBackward) backward.Add(back);

            if (hasForward && hasBackward) nearest.Add(fwd <= -back ? fwd : back);
            else if (hasForward) nearest.Add(fwd);
            else if (hasBackward) nearest.Add(back);
        }

        double f = Median(forward);
        double b = Median(backward);

        if (double.IsNaN(f)) return (long)Math.Round(b);
        if (double.IsNaN(b)) return (long)Math.Round(f);

        double period = MedianInterval(headTicks);
        double margin = ForwardPreference * (double.IsNaN(period) ? 0 : period);
        double n = Median(nearest);

        if (!double.IsNaN(n) && period > 0 && Math.Abs(n) <= margin)
            return (long)Math.Round(n);

        return (long)Math.Round(f <= Math.Abs(b) + margin ? f : b);
    }

    private static double MedianInterval(IReadOnlyList<long> ticks)
    {
        if (ticks.Count < 2)
            return double.NaN;

        var intervals = new List<double>(ticks.Count - 1);

        for (int i = 1; i < ticks.Count; i++)
            intervals.Add(ticks[i] - ticks[i - 1]);

        return Median(intervals);
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        values.Sort();
        int m = values.Count / 2;
        return values.Count % 2 == 1 ? values[m] : (values[m - 1] + values[m]) / 2.0;
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<long> values)
    {
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] <= values[i - 1])
                return false;
        }

        return true;
    }
}
