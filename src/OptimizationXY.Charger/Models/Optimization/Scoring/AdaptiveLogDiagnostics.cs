namespace OptimizationXY.Charger.Models.Optimization.Scoring;

/// <summary>
/// Measured facts about THIS log that AdaptiveScoringProfileBuilder uses to
/// decide what kind of log it is (and so how to weight the penalty scores):
/// frame counts, size, sweep/bow, out-of-roundness and defect amounts.
/// </summary>
public sealed class AdaptiveLogDiagnostics
{

    /// <summary>
    /// Gets or sets the total number of frames captured for this log.
    /// </summary>
    public int TotalFrames { get; set; }

    /// <summary>
    /// Gets or sets the number of frames that produced a usable profile.
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
    /// Gets or sets the maximum bow in millimetres: the largest distance of a slice centre from the straight end-to-end centre line.
    /// </summary>
    public double MaxBowMM { get; set; }

    /// <summary>
    /// Gets or sets the out-of-roundness of the log in millimeters, which indicates how much the log deviates from a perfect circular cross-section.
    /// </summary>
    public double OutOfRoundMM { get; set; }

    /// <summary>
    /// Gets or sets the percentage of radius cells classified LowRadius (missing wood, not wood quality).
    /// </summary>
    public double LowWoodPercent { get; set; }

    /// <summary>
    /// Gets or sets the percentage of radius cells classified SevereLowRadius.
    /// </summary>
    public double SevereLowWoodPercent { get; set; }

    /// <summary>
    /// Gets or sets the percentage of radius cells classified HighRadius (bumps above the slice median - not wood quality).
    /// </summary>
    public double HighSpotPercent { get; set; }

    /// <summary>
    /// Gets or sets the deepest low-radius error in millimetres (negative = below the slice median).
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
    /// Inch versions of the values above, for the operator display (25.4 mm per inch).
    /// DeepestLowWoodIn is reported as a positive depth.
    /// </summary>
    public double CenterlineSweepIn => CenterlineSweepMM / 25.4;
    public double MaxBowIn => MaxBowMM / 25.4;
    public double OutOfRoundIn => OutOfRoundMM / 25.4;
    public double DeepestLowWoodIn => DeepestLowWoodMM < 0.0
        ? Math.Abs(DeepestLowWoodMM) / 25.4
        : 0.0;
}
