
// One row of the processing timing report: how long a named pipeline step took.
// The report is saved with every scan so slow stages can be found.
namespace OptimizationXY.Charger.Models.Diagnostics;

/// <summary>
/// Timing for one processing step (see ScanTimeline / the pipeline timing report).
/// </summary>
public sealed class PipelineTimingEntry
{
    /// <summary>
    /// Gets or sets the name of the pipeline step.
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the elapsed time for the pipeline step in milliseconds.
    /// </summary>
    public double ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Gets the elapsed time for the pipeline step in seconds.
    /// </summary>
    public double ElapsedSeconds => ElapsedMilliseconds / 1000.0;

    /// <summary>
    /// When this step started, in milliseconds after the processing run began.
    /// Used to list the stages in the order they ran.
    /// </summary>
    public double StartOffsetMilliseconds { get; set; }

    /// <summary>
    /// 0 for a top-level stage; 1 or more for a step timed inside another step.
    /// Nested steps are part of their parent's time, so they are not added again.
    /// </summary>
    public int Depth { get; set; }
}
