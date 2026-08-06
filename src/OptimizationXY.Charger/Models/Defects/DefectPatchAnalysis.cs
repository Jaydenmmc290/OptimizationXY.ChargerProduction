namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// Represents a comprehensive analysis summary of all defect patches detected on a log.
///
/// This class aggregates statistics about defect patches including counts, sizes,
/// depths, and severity scores. It provides both individual patch references
/// (largest, worst) and aggregate metrics for understanding overall log defect severity.
///
/// Used for:
/// - Quick assessment of overall log quality
/// - Identifying critical patches needing correction
/// - Computing aggregate penalty scores
/// - Deciding if log is salvageable or must be culled
/// </summary>
public class DefectPatchAnalysis
{
    /// <summary>
    /// Gets or sets the total number of distinct defect patches identified on the log.
    ///
    /// Each patch is a contiguous region of similar defect types.
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
    /// Gets or sets a reference to the most severe defect patch.
    ///
    /// Nullable - will be null if no defects exist.
    /// Severity is typically based on error magnitude and extent.
    /// This patch requires highest priority for optimization.
    /// May differ from LargestPatch if severity scores are not size-based.
    /// </summary>
    public RadiusDefectPatch? WorstPatch { get; set; }

    /// <summary>
    /// Gets or sets the maximum (worst-case) depth of any defect patch, measured in millimeters.
    ///
    /// Represents the single most severe material loss or deficiency.
    /// Critical for determining if log meets minimum acceptable quality.
    /// Zero or near-zero values indicate only superficial defects.
    /// </summary>
    public double WorstDepthMM { get; set; }

    /// <summary>
    /// Gets or sets the average depth across all defect patches, measured in millimeters.
    ///
    /// Calculated as mean of all patch WorstErrorMM values.
    /// Provides overall indication of typical defect severity.
    /// Lower values indicate log is less severely defective.
    /// </summary>
    public double AverageDepthMM { get; set; }

    /// <summary>
    /// Gets or sets the sum of severity scores across all defect patches.
    ///
    /// Aggregates the individual SeverityScore from each patch.
    /// Provides overall quantification of total defect burden on the log.
    /// Used to determine if optimization can sufficiently improve the log.
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
    /// Must be addressable by optimization or log is not salvageable.
    /// </summary>
    public double WorstSeverityScore { get; set; }

    /// <summary>
    /// Gets a value indicating whether any defects were detected on the log.
    ///
    /// Returns true if PatchCount > 0, false otherwise.
    /// Quick check for whether the log has any defect patches.
    /// </summary>
    
}
