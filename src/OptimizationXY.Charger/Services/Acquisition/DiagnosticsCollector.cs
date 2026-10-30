using System.Collections.Concurrent;
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Services.Acquisition;

/// <summary>
/// Thread-safe acquisition counters used by the operator diagnostics display.
/// Counter names are "&lt;Head|Tail&gt;FramesReceived" and "&lt;Head|Tail&gt;FramesDropped".
/// Safe to call from the camera callback threads at the same time.
/// </summary>
public sealed class DiagnosticsCollector
{
    private readonly ConcurrentDictionary<string, long> _counters = new();

    /// <summary>Counts one frame arriving from a camera (before any checks).</summary>
    public void RecordFrameReceived(CameraPosition position) =>
        Increment($"{position}FramesReceived");

    /// <summary>Counts one frame thrown away (empty, outside a capture, over a limit, ...).</summary>
    public void RecordFrameDropped(CameraPosition position) =>
        Increment($"{position}FramesDropped");

    /// <summary>Reads a counter by name; 0 if it has never been incremented.</summary>
    public long GetCounter(string key) =>
        _counters.TryGetValue(key, out long value) ? value : 0;

    private void Increment(string key) =>
        _counters.AddOrUpdate(key, 1, static (_, current) => current + 1);
}

// (Leftover test line from setting up ClickOnce publishing; safe to remove.)
