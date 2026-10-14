using System.Diagnostics;
using System.Text;

namespace OptimizationXY.Charger.Services.Core;

/// <summary>
/// Real elapsed time of one block's scan cycle, from the PLC ScanComplete (or
/// the end of capture when ScanComplete is not required) to the verified PLC
/// write. Each step marks the moment it finished; the report shows how long
/// each step took and the running total, so the time the PLC waits for its
/// positions can be read off directly.
///
/// Lives outside PipelineState on purpose: PipelineState is cleared when
/// processing starts, after the first marks were made.
/// </summary>
public static class ScanTimeline
{
    private static readonly Lock Sync = new();
    private static readonly List<(string Name, double AtMS)> Marks = [];
    private static readonly Stopwatch Clock = new();

    /// <summary>Starts a new timeline for a new block.</summary>
    public static void Start(string firstMilestone)
    {
        lock (Sync)
        {
            Marks.Clear();
            Clock.Restart();
            Marks.Add((firstMilestone, 0.0));
        }
    }

    /// <summary>Records that a step has just finished. Ignored if no timeline is running.</summary>
    public static void Mark(string milestone)
    {
        lock (Sync)
        {
            if (!Clock.IsRunning)
                return;

            Marks.Add((milestone, Clock.Elapsed.TotalMilliseconds));
        }
    }

    /// <summary>Stops the timeline; later marks are ignored until the next Start.</summary>
    public static void Stop()
    {
        lock (Sync)
        {
            Clock.Stop();
        }
    }

    /// <summary>
    /// Report of the current timeline. Empty when nothing was recorded.
    /// </summary>
    public static string BuildReport()
    {
        lock (Sync)
        {
            if (Marks.Count < 2)
                return string.Empty;

            var report = new StringBuilder();
            report.Append(Environment.NewLine);
            report.Append("=== SCAN-TO-PLC TIMELINE (real elapsed time) ===").Append(Environment.NewLine);
            report.Append($"{"Step finished".PadRight(46)} {"Step took",10} {"Since start",12}").Append(Environment.NewLine);

            double previous = 0.0;

            foreach ((string name, double atMS) in Marks)
            {
                double stepMS = atMS - previous;
                previous = atMS;

                report.Append(
                    $"{name.PadRight(46)} {stepMS / 1000.0,8:F2} s {atMS / 1000.0,10:F2} s")
                    .Append(Environment.NewLine);
            }

            return report.ToString();
        }
    }
}
