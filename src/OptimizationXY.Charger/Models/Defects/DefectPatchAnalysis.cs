namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// Represents a comprehensive analysis summary of all defect patches detected on a log.
///
/// This class aggregates statistics about defect patches including counts, sizes,
/// depths, and severity scores. It provides both individual patch references
/// (largest, worst) and aggregate metrics for understanding overall log defect severity.
///
/// Built by DefectPatchAnalyzer from the patches made by RadiusDefectPatchBuilder.
/// Depths/errors are radius errors in millimetres and are NEGATIVE for missing
/// wood (measured radius below the slice's expected radius).
///
/// Used for:
/// - Quick assessment of overall log quality
/// - Identifying critical patches needing correction
/// - Computing aggregate penalty scores (only in the legacy PenaltyScored mode;
///   the inscribed-geometry modes keep these numbers for diagnostics only)
/// - Diagnostics saved with each scan
/// </summary>
public class DefectPatchAnalysis
{
    /// <summary>
    /// Gets or sets the total number of distinct defect patches identified on the log.
    ///
    /// Each patch is a connected region of low-radius (LowRadius/SevereLowRadius) cells.
    /// Higher count indicates more fragmented defects.
    /// </summary>
    public int PatchCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of individual defect points across all patches.
    ///
    /// Each cell/point represents a measurement location classified as defective.
    /// This is the sum of points across all patches.
    /// Larger count indicates more extensive defect coverage.
    /// </summary>
    public int TotalDefectCells { get; set; }

    /// <summary>
    /// Gets or sets the average size of defect patches, measured in number of cells.
    ///
    /// Calculated as: TotalDefectCells / PatchCount (if PatchCount > 0)
    /// Indicates typical patch size. Larger averages suggest bigger, more continuous defects.
    /// </summary>
    public double AveragePatchSizeCells { get; set; }

    /// <summary>
    /// Gets or sets a reference to the largest defect patch by cell count.
    ///
    /// Nullable - will be null if no defects exist.
    /// Useful for identifying the most extensive defect region.
    /// May differ from WorstPatch if severity isn't proportional to size.
    /// </summary>
    public RadiusDefectPatch? LargestPatch { get; set; }

    /// <summary>
    /// Gets or sets the DEEPEST defect patch: the one with the most negative
    /// <see cref="RadiusDefectPatch.WorstErrorMM"/> (not the highest severity score).
    ///
    /// Nullable - will be null if no defects exist.
    /// May differ from LargestPatch.
    /// </summary>
    public RadiusDefectPatch? WorstPatch { get; set; }

    /// <summary>
    /// Gets or sets the worst (most negative) radius error of any patch, in millimetres.
    /// For example -14.2 means the deepest missing wood is 14.2 mm below expected.
    ///
    /// Values near zero indicate only shallow defects.
    /// </summary>
    public double WorstDepthMM { get; set; }

    /// <summary>
    /// Gets or sets the average depth across all defect patches, measured in millimeters.
    ///
    /// Calculated as the mean of every patch's AverageErrorMM (negative = missing wood).
    /// Provides overall indication of typical defect severity.
    /// Lower values indicate log is less severely defective.
    /// </summary>
    public double AverageDepthMM { get; set; }

    /// <summary>
    /// Gets or sets the sum of severity scores across all defect patches.
    ///
    /// Aggregates the individual SeverityScore from each patch.
    /// Provides overall quantification of total defect burden on the log.
    /// </summary>
    public double TotalSeverityScore { get; set; }

    /// <summary>
    /// Gets or sets the average severity score across all defect patches.
    ///
    /// Calculated as: TotalSeverityScore / PatchCount (if PatchCount > 0)
    /// Indicates typical severity per patch.
    /// Useful for comparing logs of different sizes or defect counts.
    /// </summary>
    public double AverageSeverityScore { get; set; }

    /// <summary>
    /// Gets or sets the severity score of the single worst defect patch.
    ///
    /// The highest individual patch severity score.
    /// Identifies the critical problem area requiring most aggressive correction.
    /// </summary>
    public double WorstSeverityScore { get; set; }

    // (A "HasDefects" property used to be documented here but no longer exists;
    // use PatchCount > 0 to check whether any defect patches were found.)
    
}
