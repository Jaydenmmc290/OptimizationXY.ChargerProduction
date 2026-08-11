using System.Collections.Concurrent;
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Services.Acquisition;

/// <summary>
/// Thread-safe acquisition counters used by the operator diagnostics display.
/// </summary
public sealed class DiagnosticsCollector
{
    private readonly ConcurrentDictionary<string, long> _counters = new();

    public void RecordFrameReceived(CameraPosition position) =>
        Increment($"{position}FramesReceived");

    public void RecordFrameDropped(CameraPosition position) =>
        Increment($"{position}FramesDropped");

    public long GetCounter(string key) =>
        _counters.TryGetValue(key, out long value) ? value : 0;

    private void Increment(string key) =>
        _counters.AddOrUpdate(key, 1, static (_, current) => current + 1);
}

//Test for Click Once
