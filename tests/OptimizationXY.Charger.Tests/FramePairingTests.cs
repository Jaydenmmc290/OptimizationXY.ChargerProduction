using System.Diagnostics;
using OptimizationXY.Charger.Services.Acquisition;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// Head/tail frame pairing: one extra or missing frame on one camera must not
/// shift every pair after it (Rebuild F-02, F-45, F-47).
/// </summary>
public class FramePairingTests
{
    private static long Ms(double ms) => (long)(ms * Stopwatch.Frequency / 1000.0) + 1_000_000;

    /// <summary>n frames every periodMs starting at startMs.</summary>
    private static List<long> Stream(int n, double periodMs, double startMs = 0) =>
        [.. Enumerable.Range(0, n).Select(i => Ms(startMs + i * periodMs))];

    private static List<long> Numbers(int n, long first = 1) =>
        [.. Enumerable.Range(0, n).Select(i => first + i)];

    [Fact]
    public void TimePairingMatchesEveryFrameWhenTheTailArrivesALittleLater()
    {
        List<long> head = Stream(90, 33.3);
        List<long> tail = Stream(90, 33.3, startMs: 6);

        FramePairingResult r = FramePairing.Pair(head, tail, Numbers(90), Numbers(90), pulseTriggered: false);

        Assert.StartsWith("time", r.Method);
        Assert.Equal(90, r.Pairs.Count);
        Assert.All(r.Pairs, p => Assert.Equal(p.HeadIndex, p.TailIndex));
    }

    [Fact]
    public void TwoExtraTailFramesAtTheStartAreLeftOutInsteadOfShiftingEveryPair()
    {
        // The tail camera delivered two frames before the head camera's first.
        List<long> head = Stream(90, 33.3, startMs: 66.6);
        List<long> tail = Stream(92, 33.3, startMs: 2);

        FramePairingResult r = FramePairing.Pair(head, tail, Numbers(90), Numbers(92), pulseTriggered: false);

        Assert.Equal(90, r.Pairs.Count);
        Assert.Equal([0, 1], r.UnpairedTail);
        Assert.All(r.Pairs, p => Assert.Equal(p.HeadIndex + 2, p.TailIndex));

        // Capture order would have put every pair two frames (about 8 degrees) apart.
        FramePairingResult order = FramePairing.PairByOrder(90, 92);
        Assert.All(order.Pairs, p => Assert.Equal(p.HeadIndex, p.TailIndex));
    }

    [Fact]
    public void PulsePairingUsesTheFrameCountersAcrossAMissingFrame()
    {
        // Encoder mode: tail counter started one higher; the tail lost pulse 40.
        List<long> head = Stream(90, 33.3);
        List<long> headNumbers = Numbers(90, first: 1);

        List<long> tail = [];
        List<long> tailNumbers = [];

        for (int i = 0; i < 90; i++)
        {
            if (i == 40)
                continue;

            tail.Add(Ms(i * 33.3 + 4));
            tailNumbers.Add(i + 2);
        }

        FramePairingResult r = FramePairing.Pair(head, tail, headNumbers, tailNumbers, pulseTriggered: true);

        Assert.StartsWith("pulse", r.Method);
        Assert.Equal(1, r.NumberOffset);
        Assert.Equal(89, r.Pairs.Count);
        Assert.Equal([40], r.UnpairedHead);
        Assert.All(r.Pairs, p => Assert.Equal(headNumbers[p.HeadIndex] + 1, tailNumbers[p.TailIndex]));
    }

    [Fact]
    public void WithoutCaptureTimesItFallsBackToCaptureOrder()
    {
        List<long> zeros = [.. Enumerable.Repeat(0L, 30)];

        FramePairingResult r = FramePairing.Pair(zeros, zeros, Numbers(30), Numbers(30), pulseTriggered: false);

        Assert.StartsWith("order", r.Method);
        Assert.Equal(30, r.Pairs.Count);
    }
}
