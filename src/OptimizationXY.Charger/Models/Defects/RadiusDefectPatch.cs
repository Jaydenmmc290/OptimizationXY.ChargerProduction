namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// Represents a contiguous region or patch of defects on the log surface.
///
/// A patch is a connected cluster of low-radius points (LowRadius and/or
/// SevereLowRadius) that touch each other within a Z and angle tolerance.
/// Multiple patches across the log comprise the complete RadiusDefectMap.
/// Patches are useful for:
/// - Locating defect clusters
/// - Calculating regional severity
/// - Feeding the legacy PenaltyScored optimizer
/// - Prioritizing which defects to correct
/// </summary>
public class RadiusDefectPatch
{
    /// <summary>
    /// Gets or sets a unique identifier for this defect patch within the log.
    ///
    /// Used to reference and track individual patches in analysis and optimization.
    /// Assigned sequentially from 1 as patches are found.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the classification type of defects in this patch.
    ///
    /// SevereLowRadius if ANY point in the patch is severe, otherwise LowRadius.
    ///
    ///
    /// </summary>
    public RadiusDefectType DefectType { get; set; }

    /// <summary>
    /// Gets or sets the collection of individual defect points that comprise this patch.
    ///
    /// These points define the spatial extent and severity of the defect region.
    /// Initialized to empty list by default.
    /// Use CellCount property to get the point count.
    /// </summary>
    public List<RadiusDefectPoint> Points { get; set; } = [];

    /// <summary>
    /// Gets or sets the minimum axial position (start of patch along log length), measured in millimeters.
    ///
    /// Defines the beginning of the patch's extent along the log axis.
    /// Lower Z value of the patch boundaries.
    /// </summary>
    public double StartZMM { get; set; }

    /// <summary>
    /// Gets or sets the maximum axial position (end of patch along log length), measured in millimeters.
    ///
    /// Defines the end of the patch's extent along the log axis.
    /// Higher Z value of the patch boundaries.
    /// </summary>
    public double EndZMM { get; set; }

    /// <summary>
    /// Gets or sets the starting circumferential angle of the patch, measured in degrees.
    ///
    /// The smallest angle bin (0-360) of any point in the patch.
    /// NOTE: this is a plain minimum, so a patch that crosses 0 degrees (e.g.
    /// 350 to 10) is reported as roughly 0 to 360.
    /// </summary>
    public double StartAngleDeg { get; set; }

    /// <summary>
    /// Gets or sets the ending circumferential angle of the patch, measured in degrees.
    ///
    /// The largest angle bin (0-360) of any point in the patch (see StartAngleDeg note).
    /// </summary>
    public double EndAngleDeg { get; set; }

    /// <summary>
    /// Gets or sets the average radius error across all points in this patch, measured in millimeters.
    ///
    /// Calculated as the mean of RadiusErrorMM for all points in the patch.
    /// Provides a quick indicator of typical defect severity in the patch.
    /// Used in penalty calculations and defect scoring.
    /// </summary>
    public double AverageErrorMM { get; set; }

    /// <summary>
    /// Gets or sets the maximum (worst-case) radius error in this patch, measured in millimeters.
    ///
    /// The most NEGATIVE error among the points (the deepest missing wood).
    /// Identifies the most severe location within the patch.
    /// </summary>
    public double WorstErrorMM { get; set; }

    /// <summary>
    /// Gets or sets the computed severity score for this patch.
    ///
    /// Higher values indicate more severe defects requiring more aggressive correction.
    /// Calculated as CellCount x |AverageErrorMM| (bigger and deeper = higher).
    /// Used for prioritization and optimization weighting.
    /// </summary>
    public double SeverityScore { get; set; }

    /// <summary>
    /// Gets the number of individual defect points (cells) comprising this patch.
    ///
    /// Returns Points.Count.
    /// Larger cell counts indicate more extensive defect coverage.
    /// </summary>
    public int CellCount => Points.Count;
}
