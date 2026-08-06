
/// <summary>
/// Represents a timing entry for a specific step in a processing pipeline.
/// </summary>
namespace OptimizationXY.Charger.Models.Diagnostics;

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
}
