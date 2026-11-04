using System.Globalization;

namespace OptimizationXY.Charger.Services.Plc;

/// <summary>
/// The XY PLC waits a fixed time after its own ScanComplete for this program's
/// result; after that it positions the block on its own fallback (single-point
/// lasers plus its correction offsets) and a result written later no longer
/// decides where the block goes. This turns the measured ScanComplete-to-write
/// time into the line logged on every block.
/// </summary>
public static class PlcResultWindow
{
    /// <summary>
    /// The log line for a result written <paramref name="seconds"/> after
    /// ScanComplete. <paramref name="late"/> is true at or past
    /// <paramref name="warnAtSeconds"/>.
    /// </summary>
    public static string Describe(double seconds, double warnAtSeconds, double windowSeconds, out bool late)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        string s = seconds.ToString("0.0", ci);
        string w = windowSeconds.ToString("0", ci);

        late = warnAtSeconds > 0 && seconds >= warnAtSeconds;

        if (windowSeconds > 0 && seconds >= windowSeconds)
        {
            return $"Result written {s} s after ScanComplete: past the PLC's {w} s wait, so the PLC's own fallback may already have positioned this block.";
        }

        if (late)
        {
            return $"Result written {s} s after ScanComplete: close to the PLC's {w} s wait.";
        }

        return $"Result written {s} s after ScanComplete (PLC waits {w} s).";
    }
}
