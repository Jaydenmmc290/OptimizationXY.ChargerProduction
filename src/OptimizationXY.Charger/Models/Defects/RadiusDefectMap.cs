namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// Represents a complete map of all radius defects detected across an entire log.
///
/// This class aggregates all defect points from all cross-sections and axial positions,
/// providing a comprehensive view of the log's defect distribution. It also provides
/// summary statistics including min/max errors and counts of each defect type.
///
/// Built by RadiusDefectMapBuilder: every valid cell of the binned radius field
/// is compared with the MEDIAN radius of its Z slice and classified.
///
/// The defect map is used for:
/// - Understanding overall log quality
/// - Feeding the legacy PenaltyScored optimizer (diagnostic only in the
///   inscribed-geometry modes)
/// - Calculating penalty scores
/// - Visualizing defect distribution
/// </summary>
public class RadiusDefectMap
{
    /// <summary>
    /// Gets or sets the complete collection of all defect points across the entire log.
    ///
    /// Each point represents a single measurement location on the log surface with
    /// associated error and defect type classification.
    /// Initialized to empty list by default.
    /// Stored as a flat list, slice by slice along the log.
    /// </summary>
    public List<RadiusDefectPoint> Points { get; set; } = [];

    /// <summary>
    /// Gets the minimum radius error observed across all points in the map, measured in millimeters.
    ///
    /// Returns 0 if no points are present.
    /// Negative values indicate material loss (wane).
    /// (This is the deepest low point, i.e. the WORST case.)
    /// </summary>
    public double MinErrorMM => Points.Count == 0 ? 0 : Points.Min(p => p.RadiusErrorMM);

    

    /// <summary>
    /// Gets the total count of points classified as "Good" (within acceptable limits).
    ///
    /// Higher values indicate better overall log quality.
    /// Useful for computing quality percentage metrics.
    /// </summary>
    public int GoodCount => Points.Count(p => p.DefectType == RadiusDefectType.Good);

    /// <summary>
    /// Gets the total count of points classified as "LowRadius" (minor material deficiency).
    ///
    /// These points represent wane or material loss but not at severe levels.
    /// </summary>
    public int LowCount => Points.Count(p => p.DefectType == RadiusDefectType.LowRadius);

    /// <summary>
    /// Gets the total count of points classified as "SevereLowRadius" (severe material deficiency).
    ///
    /// These points represent severe wane or major material loss.
    /// </summary>
    public int SevereLowCount => Points.Count(p => p.DefectType == RadiusDefectType.SevereLowRadius);

    /// <summary>
    /// Gets the total count of points classified as "HighRadius" (excess material).
    ///
    /// These points represent measurements where radius exceeds expected value.
    /// Generally less problematic than low-radius defects.
    /// Usually bumps, knots, bark or a laser reflection rather than missing wood.
    /// </summary>
    public int HighCount => Points.Count(p => p.DefectType == RadiusDefectType.HighRadius);
}
