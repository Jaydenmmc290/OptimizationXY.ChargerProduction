namespace OptimizationXY.Charger.Models.Optimization.Scoring;

/// <summary>
/// Represents the diagnostic information for an adaptive log, including frame counts, log dimensions, and defect metrics.
/// </summary>
public sealed class AdaptiveLogDiagnostics
{

    /// <summary>
    /// Gets or sets the total number of frames in the adaptive log.
    /// </summary>
    public int TotalFrames { get; set; }

    /// <summary>
    /// Gets or sets the number of valid frames in the adaptive log.
    /// </summary>
    public int ValidFrames { get; set; }

    /// <summary>
    /// Gets or sets the length of the log in millimeters.
    /// </summary>
    public double LogLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the average diameter of the log in millimeters.
    /// </summary>
    public double AverageDiameterMM { get; set; }

    /// <summary>
    /// Gets or sets the centerline sweep of the log in millimeters, which indicates the deviation of the log's centerline from a straight line.
    /// </summary>
    public double CenterlineSweepMM { get; set; }


    /// <summary>
    /// Gets or sets the maximum bow of the log in millimeters, which indicates the maximum deviation of the log's surface from a straight line.
    /// </summary>
    public double MaxBowMM { get; set; }

    /// <summary>
    /// Gets or sets the out-of-roundness of the log in millimeters, which indicates how much the log deviates from a perfect circular cross-section.
    /// </summary>
    public double OutOfRoundMM { get; set; }

    /// <summary>
    /// Gets or sets the percentage of low wood in the log, which indicates the proportion of the log that has low-quality wood.
    /// </summary>
    public double LowWoodPercent { get; set; }

    /// <summary>
    /// Gets or sets the percentage of severe low wood in the log, which indicates the proportion of the log that has severely low-quality wood.
    /// </summary>
    public double SevereLowWoodPercent { get; set; }

    /// <summary>
    /// Gets or sets the percentage of high spots in the log, which indicates the proportion of the log that has high-quality wood.
    /// </summary>
    public double HighSpotPercent { get; set; }

    /// <summary>
    /// Gets or sets the deepest low wood measurement in millimeters, which indicates the maximum depth of low-quality wood in the log.
    /// </summary>
    public double DeepestLowWoodMM { get; set; }


    /// <summary>
    /// Gets or sets the worst patch severity score of the log, which indicates the severity of the worst defect patch in the log.
    /// </summary>
    public double WorstPatchSeverityScore { get; set; }

    /// <summary>
    /// Gets or sets the number of cells in the largest defect patch of the log, which indicates the size of the largest defect area in terms of cell count.
    /// </summary>
    public int LargestPatchCells { get; set; }


    /// <summary>
    /// Gets the centerline sweep of the log in inches, which is derived from the centerline sweep in millimeters.
    /// </summary>
    public double CenterlineSweepIn => CenterlineSweepMM / 25.4;
    public double MaxBowIn => MaxBowMM / 25.4;
    public double OutOfRoundIn => OutOfRoundMM / 25.4;
    public double DeepestLowWoodIn => DeepestLowWoodMM < 0.0
        ? Math.Abs(DeepestLowWoodMM) / 25.4
        : 0.0;
}
